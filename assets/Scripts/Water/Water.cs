using System;
using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using GraphicsTesting.assets.Scripts.Utility;
using Wave = Godot.Collections.Dictionary<Godot.StringName, float>;
[Tool]
public partial class Water : MeshInstance3D {
	[Export] public float Time = 0.0f;

	


	private ShaderMaterial _material;
	[ExportGroup("Materials")] [Export] public ShaderMaterial _sumOfSinesMat = null;
	[Export] public ShaderMaterial _sumOfSinesTextureMat = null;
	[ExportGroup("")] private PlaneMesh plane = null;
	[Export] public Vector2 size;
	[Export] public int subdivide;
	
	[Export] public bool useBuffers = true;
	[Export] public bool render = true;
	[Export] public bool simulate = false;

	private ImageTexture texture;
	[Export] public uint waveCount = 42;
	
	[ExportGroup("Wave Parameters")]
	[Export]
	public float baseAmplitude = 1.0f;

	[Export] public float baseFrequency = 0.1f;
	[Export] public float gain = 1.18f;
	[Export] public float lacunarity = 0.82f;
	[Export] public float phase_modifier = 1.0f;
	private float prevAmp = 0.0f;
	private float prevFreq = 0.0f;
	private float prevGain = 0.0f;
	private float prevLac = 0.0f;
	private float prevPhase = 0.0f;
	private float[] prevSeed;

	[ExportGroup("")]
	[ExportToolButton("Regenerate Mesh")]
	public Callable regenerate => Callable.From(RegenerateMesh);
	[ExportToolButton("Regenerate Waves")]
	public Callable regenWaves => Callable.From(RegenerateWaves);
	[ExportToolButton("Regenerate Shader")]
	public Callable regenShad => Callable.From(RegenerateShader);

	
	// compute shader stuff
	private ComputeHandler compHandler;

	public void RegenerateWaves() {
		plane = null;
		GenerateMesh();
		GenerateSineWaves();
	}
	
	public void RegenerateShader() {
		compHandler.AddShader("buffer_gen",GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwavegen.glsl"));
		compHandler.AddShader("texture_gen",GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwave_texgen.glsl"));
	}

	public void ToggleBuffers(bool value) {
		useBuffers = value;
	}

	#region Overrides

	public override void _Ready() {
		compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		
		
		_material = _sumOfSinesMat;
		GenerateSineWaves();
		if (Engine.IsEditorHint()) return;
		GenerateMesh();
	}

	public override void _Process(double delta) {
		if (compHandler == null) {
			compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		}
		if (Engine.IsEditorHint()) {
			if (plane != null && !render) {
				plane = null;
				Mesh = null;
			} else if (plane == null && render) {
				GenerateMesh();
			}
			
			if (_material == null) return;

			_material.SetShaderParameter("size", size);

			if (!simulate) {
				return;
			}
		}

		Time += (float)delta;
		_material.SetShaderParameter("time", Time);
	}

	public override void _ExitTree() {
		compHandler?.Close();
		base._ExitTree();
	}

	#endregion

	private void GenerateSineWaves() {
		if (compHandler == null) {
			compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		}
		if (!compHandler.HasUniform("paramBuffer")) {
			compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
		}
		
		if (prevAmp != baseAmplitude || prevFreq != baseFrequency || prevPhase != phase_modifier || prevGain != gain || prevLac != lacunarity) {
			var inputUniforms = new byte[32];
			
			float[] inputs = [baseAmplitude, baseFrequency, phase_modifier, lacunarity, gain];
			Buffer.BlockCopy( inputs, 0, inputUniforms, 0, sizeof(float) * 5);
		
			compHandler.SetBuffer("paramBuffer", 20u, inputUniforms);
			prevAmp = baseAmplitude;
			prevFreq = baseFrequency;
			prevPhase = phase_modifier;
			prevGain = gain;
			prevLac = lacunarity;
		}
		
		if (useBuffers) {
			GenerateWaveBuffer(PushConstants());
		} else {
			GenerateWaveTexture(PushConstants());
		}
	}

	private byte[] PushConstants(bool newSeed = true) {
		var rng = new RandomNumberGenerator();
		byte[] pushConstants = new byte[16];
		float[] seedMod = [rng.RandfRange(0f, 4f * Mathf.Pi)];
		if (!newSeed) {
			seedMod = prevSeed;
		} else {
			prevSeed = seedMod;
		}
		uint[] inputWaveCount = [waveCount];
		Buffer.BlockCopy( inputWaveCount, 0, pushConstants, 0, sizeof(uint));
		Buffer.BlockCopy( seedMod, 0, pushConstants, sizeof(uint), sizeof(float));
		return pushConstants;
	}

	private void GenerateWaveBuffer(byte[] pushConstants) {
		if (!compHandler.HasShader("buffer_gen")) {
			compHandler.AddShader("buffer_gen", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwavegen.glsl"));
			if (!compHandler.HasUniform("paramBuffer")) {
				compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
			}
			compHandler.AssignUniform("buffer_gen", "paramBuffer", 0, 0);
		}
		
		if (!compHandler.HasUniform("waveBuffer")) {
			RDUniform buffer = new RDUniform() {
				UniformType = RenderingDevice.UniformType.StorageBuffer
			};
			buffer.AddId(compHandler.CreateBuffer("waveBuffer", RenderingDevice.UniformType.StorageBuffer, 24 * waveCount, "buffer_gen", 0, 1));
			
		} else {
			Rid new_buf = compHandler.SetBuffer("waveBuffer", waveCount * 24);
			if (new_buf.IsValid) {
				
			}
		}
		
		compHandler.Dispatch("buffer_gen", waveCount / 2, 1, 1, pushConstants);
		_material.SetShaderBufferRaw("waveBuffer", compHandler.GetBufferData("waveBuffer"));
	}
	
	private void GenerateWaveTexture(byte[] pushConstants) {
		if (!compHandler.HasShader("texture_gen")) {
			compHandler.AddShader("texture_gen", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwave_texgen.glsl"));
			if (!compHandler.HasUniform("paramBuffer")) {
				compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
			}
			compHandler.AssignUniform("texture_gen", "paramBuffer", 0, 0);
		}
		
		if (!compHandler.HasUniform("waveTexture")) {
			Texture2Drd texUniform = new Texture2Drd();
			texUniform.TextureRdRid = compHandler.CreateTexture("waveTexture", waveCount, 2);
			RenderingServer.GlobalShaderParameterSet("waveTexture", texUniform);
			compHandler.AssignUniform("texture_gen", "waveTexture", 0, 1);
		}
		
		compHandler.Dispatch("texture_gen", waveCount / 2, 1, 1, pushConstants);
	}


	#region Helpers

	public void RegenerateMesh() {
		plane = null;
		GenerateMesh();
	}

	private void GenerateMesh() {
		if (plane != null) return;
		if (useBuffers) {
			_material = _sumOfSinesMat;
			GenerateWaveBuffer(PushConstants(false));
		} else {
			_material = _sumOfSinesTextureMat;
			_material.SetShaderParameter("waveCount", waveCount);
			GenerateWaveTexture(PushConstants(false));
		}
		plane = new() {
			Size = size,
			SubdivideWidth = subdivide,
			SubdivideDepth = subdivide
		};
		Mesh = plane;
		Mesh.SurfaceSetMaterial(0, _material);
	}

	#endregion
}