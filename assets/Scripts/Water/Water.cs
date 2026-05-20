using System;
using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using GraphicsTesting.assets.Scripts.Utility;
using GraphicsTesting.Libraries.ComputeShaderHandling;
using Wave = Godot.Collections.Dictionary<Godot.StringName, float>;
[Tool]
public partial class Water : MeshInstance3D {
	[Export] public float Time = 0.0f;

	


	private ShaderMaterial _material;
	[Export] public ShaderMaterial SumOfSinesTextureMat = null;
	[ExportGroup("")]
	[Export] public Vector2 Size;
	[Export] public int Subdivide;
	private PlaneMesh _plane;
	
	[Export] public bool Render = true;
	
	

	private ImageTexture _texture;
	[Export] public uint WaveCount = 42;
	
	[ExportGroup("Wave Parameters")]
	[Export]
	public float BaseAmplitude = 1.0f;

	[Export] public float BaseFrequency = 0.1f;
	[Export] public float Gain = 1.18f;
	[Export] public float Lacunarity = 0.82f;
	
	private float _prevAmp = 0.0f;
	private float _prevFreq = 0.0f;
	private float _prevGain = 0.0f;
	private float _prevLac = 0.0f;
	private float _chop = 1.0f;
	private float _prevSeed;

	private Texture2Drd wave_tex;

	[Export]
	public float ChopModifier {
		set {
			_material?.SetShaderParameter("chopMod", value);
			_chop = value;
		}
		get => _chop;
	}

	[ExportGroup("")]
	[ExportToolButton("Regenerate Waves")]
	public Callable RegenWaves => Callable.From(RegenerateWaves);
	private bool _simulate = true;
	[ExportToolButton("Simulate")]
	private Callable Simulate => Callable.From(() => (_simulate = !_simulate));

	
	// compute shader stuff
	private ComputeShader textureGen = new("res://assets/Shaders/Compute/GLSL/sinwave_texgen_adv.glsl");

	public void RegenerateWaves() {
		GenerateMesh();
		GenerateSineWaves();
	}


	#region Overrides

	public override void _Ready() {
		textureGen.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32u, 0,0);
		textureGen.CreateTexture("waveTexture", WaveCount, 2, 0, 1);
		
		ComputeShader.BindTextureParameter("waveTexture", 
			Callable.From(
				(Texture2Drd tex_uniform) => {
					wave_tex = tex_uniform;
					RenderingServer.GlobalShaderParameterSet("waveTexture", wave_tex);
				}));
		
		_material = SumOfSinesTextureMat;
		GenerateSineWaves();
		if (Engine.IsEditorHint()) return;
		GenerateMesh();
	}

	public override void _Process(double delta) {
		if (Engine.IsEditorHint()) {
			if (_plane != null && !Render) {
				_plane = null;
				Mesh = null;
			} else if (_plane == null && Render) {
				GenerateMesh();
			}
			
			if (_material == null) return;

			if (!_simulate) {
				return;
			}
		}


		Time += (float)delta;
		_material.SetShaderParameter("time", Time);
	}

	public override void _ExitTree() {
		textureGen.Close();
		base._ExitTree();
	}

	#endregion

	private void GenerateSineWaves() {
		if (_prevAmp != BaseAmplitude || _prevFreq != BaseFrequency || _prevGain != Gain || _prevLac != Lacunarity) {
			var inputUniforms = new byte[32];
			
			float[] inputs = [BaseAmplitude, BaseFrequency, Lacunarity, Gain];
			Buffer.BlockCopy( inputs, 0, inputUniforms, 0, sizeof(float) * 4);
		
			ComputeShader.SetBuffer("paramBuffer", inputUniforms);
			_prevAmp = BaseAmplitude;
			_prevFreq = BaseFrequency;
			_prevGain = Gain;
			_prevLac = Lacunarity;
		}
		
		ComputeShader.SetTextureSize("waveTexture", WaveCount, 2);
		_material.SetShaderParameter("wave_count", WaveCount);
		new ComputePlan().AddShader(textureGen, WaveCount / 2, 1, 1, GenPushConstants()).Dispatch();
		//textureGen.Dispatch(WaveCount / 2, 1, 1, GenPushConstants());
	}

	private ByteBuffer GenPushConstants(bool newSeed = true) {
		var rng = new RandomNumberGenerator();
		float seedMod = rng.RandfRange(0f, 2f * Mathf.Pi);
		if (!newSeed) {
			seedMod = _prevSeed;
		} else {
			_prevSeed = seedMod;
		}
		return new ByteBuffer().Add(WaveCount).Add(seedMod);
	}

	#region Helpers

	private void GenerateMesh() {
		_material = SumOfSinesTextureMat;
		_plane = new() {
			Size = Size,
			SubdivideWidth = Subdivide,
			SubdivideDepth = Subdivide
		};
		Mesh = _plane;
		Mesh?.SurfaceSetMaterial(0, _material);
	}

	#endregion
}