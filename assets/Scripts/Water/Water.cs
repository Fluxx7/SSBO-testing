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
	[ExportGroup("Materials")] [Export] public ShaderMaterial SumOfSinesMat = null;
	[Export] public ShaderMaterial SumOfSinesTextureMat = null;
	[ExportGroup("")] private PlaneMesh _plane = null;
	[Export] public Vector2 Size;
	[Export] public int Subdivide;
	
	[Export] public bool UseBuffers = true;
	[Export] public bool Render = true;
	
	

	private ImageTexture _texture;
	[Export] public uint WaveCount = 42;
	
	[ExportGroup("Wave Parameters")]
	[Export]
	public float BaseAmplitude = 1.0f;

	[Export] public float BaseFrequency = 0.1f;
	[Export] public float Gain = 1.18f;
	[Export] public float Lacunarity = 0.82f;
	[Export] public float BasePhase = 1.0f;
	[Export] public float PhaseModifier = 1.09f;
	
	private float _prevAmp = 0.0f;
	private float _prevFreq = 0.0f;
	private float _prevGain = 0.0f;
	private float _prevLac = 0.0f;
	private float _prevPhase = 0.0f;
	private float _prevPhaseMod = 0.0f;
	private float _chop = 1.0f;
	private float[] _prevSeed;
	

	[Export]
	public float ChopModifier {
		set {
			_material?.SetShaderParameter("chopMod", value);
			_chop = value;
		}
		get => _chop;
	}

	[ExportGroup("")]
	[ExportToolButton("Regenerate Mesh")]
	public Callable Regenerate => Callable.From(RegenerateMesh);
	[ExportToolButton("Regenerate Waves")]
	public Callable RegenWaves => Callable.From(RegenerateWaves);
	[ExportToolButton("Regenerate Shader")]
	public Callable RegenShad => Callable.From(RegenerateShader);
	private bool _simulate = true;
	[ExportToolButton("Simulate")]
	private Callable Simulate => Callable.From(() => (_simulate = !_simulate));

	
	// compute shader stuff
	private ComputeHandler _compHandler;

	public void RegenerateWaves() {
		_plane = null;
		GenerateMesh();
		GenerateSineWaves();
	}
	
	public void RegenerateShader() {
		_compHandler.AddShader("buffer_gen",GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwavegen.glsl"));
		_compHandler.AddShader("texture_gen",GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwave_texgen.glsl"));
	}

	public void ToggleBuffers(bool value) {
		UseBuffers = value;
	}

	#region Overrides

	public override void _Ready() {
		_compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		
		
		_material = SumOfSinesMat;
		GenerateSineWaves();
		if (Engine.IsEditorHint()) return;
		GenerateMesh();
	}

	public override void _Process(double delta) {
		if (_compHandler == null) {
			_compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		}
		if (Engine.IsEditorHint()) {
			if (_plane != null && !Render) {
				_plane = null;
				Mesh = null;
			} else if (_plane == null && Render) {
				GenerateMesh();
			}
			
			if (_material == null) return;

			_material.SetShaderParameter("size", Size);

			if (!_simulate) {
				return;
			}
		}

		Time += (float)delta;
		_material.SetShaderParameter("time", Time);
	}

	public override void _ExitTree() {
		_compHandler?.Close();
		base._ExitTree();
	}

	#endregion

	private void GenerateSineWaves() {
		if (_compHandler == null) {
			_compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		}
		if (!_compHandler.HasUniform("paramBuffer")) {
			_compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
		}
		
		if (_prevAmp != BaseAmplitude || _prevFreq != BaseFrequency || _prevPhaseMod != PhaseModifier || _prevPhase != BasePhase || _prevGain != Gain || _prevLac != Lacunarity) {
			var inputUniforms = new byte[32];
			
			float[] inputs = [BaseAmplitude, BaseFrequency, BasePhase, PhaseModifier, Lacunarity, Gain];
			Buffer.BlockCopy( inputs, 0, inputUniforms, 0, sizeof(float) * 6);
		
			_compHandler.SetBuffer("paramBuffer", 24u, inputUniforms);
			_prevAmp = BaseAmplitude;
			_prevFreq = BaseFrequency;
			_prevPhase = BasePhase;
			_prevPhaseMod = PhaseModifier;
			_prevGain = Gain;
			_prevLac = Lacunarity;
		}
		
		if (UseBuffers) {
			GenerateWaveBuffer(PushConstants());
		} else {
			GenerateWaveTexture(PushConstants());
		}
	}

	private byte[] PushConstants(bool newSeed = true) {
		var rng = new RandomNumberGenerator();
		byte[] pushConstants = new byte[16];
		float[] seedMod = [rng.RandfRange(0f, 2f * Mathf.Pi)];
		if (!newSeed) {
			seedMod = _prevSeed;
		} else {
			_prevSeed = seedMod;
		}
		uint[] inputWaveCount = [WaveCount];
		Buffer.BlockCopy( inputWaveCount, 0, pushConstants, 0, sizeof(uint));
		Buffer.BlockCopy( seedMod, 0, pushConstants, sizeof(uint), sizeof(float));
		return pushConstants;
	}

	private void GenerateWaveBuffer(byte[] pushConstants) {
		if (!_compHandler.HasShader("buffer_gen")) {
			_compHandler.AddShader("buffer_gen", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwavegen.glsl"));
			if (!_compHandler.HasUniform("paramBuffer")) {
				_compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
			}
			_compHandler.AssignUniform("buffer_gen", "paramBuffer", 0, 0);
		}
		
		if (!_compHandler.HasUniform("waveBuffer")) {
			RDUniform buffer = new RDUniform() {
				UniformType = RenderingDevice.UniformType.StorageBuffer
			};
			buffer.AddId(_compHandler.CreateBuffer("waveBuffer", RenderingDevice.UniformType.StorageBuffer, 24 * WaveCount, "buffer_gen", 0, 1));
			
		} else {
			Rid newBuf = _compHandler.SetBuffer("waveBuffer", WaveCount * 24);
			if (newBuf.IsValid) {
				
			}
		}
		
		_compHandler.Dispatch("buffer_gen", WaveCount / 2, 1, 1, pushConstants);
		_material.SetShaderBufferRaw("waveBuffer", _compHandler.GetBufferData("waveBuffer"));
	}
	
	private void GenerateWaveTexture(byte[] pushConstants) {
		if (!_compHandler.HasShader("texture_gen")) {
			_compHandler.AddShader("texture_gen", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwave_texgen.glsl"));
			if (!_compHandler.HasUniform("paramBuffer")) {
				_compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
			}
			_compHandler.AssignUniform("texture_gen", "paramBuffer", 0, 0);
		}
		
		if (!_compHandler.HasUniform("waveTexture")) {
			Texture2Drd texUniform = new Texture2Drd();
			texUniform.TextureRdRid = _compHandler.CreateTexture("waveTexture", WaveCount, 2);
			RenderingServer.GlobalShaderParameterSet("waveTexture", texUniform);
			_compHandler.AssignUniform("texture_gen", "waveTexture", 0, 1);
		}
		
		_compHandler.Dispatch("texture_gen", WaveCount / 2, 1, 1, pushConstants);
	}


	#region Helpers

	public void RegenerateMesh() {
		_plane = null;
		GenerateMesh();
	}

	private void GenerateMesh() {
		if (_plane != null) return;
		if (UseBuffers) {
			_material = SumOfSinesMat;
			GenerateWaveBuffer(PushConstants(false));
		} else {
			_material = SumOfSinesTextureMat;
			_material.SetShaderParameter("waveCount", WaveCount);
			GenerateWaveTexture(PushConstants(false));
		}
		_plane = new() {
			Size = Size,
			SubdivideWidth = Subdivide,
			SubdivideDepth = Subdivide
		};
		Mesh = _plane;
		Mesh.SurfaceSetMaterial(0, _material);
	}

	#endregion
}