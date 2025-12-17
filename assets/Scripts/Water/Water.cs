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
	[ExportGroup("Materials")] [Export] public ShaderMaterial SumOfSinesMat = null;
	[Export] public ShaderMaterial SumOfSinesTextureMat = null;
	[ExportGroup("")]
	[Export] private Mesh _plane;
	
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
	[ExportToolButton("Regenerate Waves")]
	public Callable RegenWaves => Callable.From(RegenerateWaves);
	private bool _simulate = true;
	[ExportToolButton("Simulate")]
	private Callable Simulate => Callable.From(() => (_simulate = !_simulate));

	
	// compute shader stuff
	private ComputeShader bufferGen = new("res://assets/Shaders/Compute/GLSL/sinwavegen.glsl");
	private ComputeShader textureGen = new("res://assets/Shaders/Compute/GLSL/sinwave_texgen.glsl");

	public void RegenerateWaves() {
		GenerateMesh();
		GenerateSineWaves();
	}

	public void ToggleBuffers(bool value) {
		UseBuffers = value;
	}

	#region Overrides

	public override void _Ready() {
		bufferGen.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32u, 0,0);
		bufferGen.CreateBuffer("waveBuffer", RenderingDevice.UniformType.StorageBuffer, 24 * WaveCount, 0, 1);
		
		textureGen.AssignUniform("paramBuffer", 0,0);
		textureGen.CreateTexture("waveTexture", WaveCount, 2, 0, 1);
		
		textureGen.BindTextureParameter("waveTexture", 
			Callable.From(
				(Texture2Drd texUniform) => 
				RenderingServer.GlobalShaderParameterSet("waveTexture", texUniform)));
		
		_material = SumOfSinesMat;
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
		bufferGen.Close();
		textureGen.Close();
		base._ExitTree();
	}

	#endregion

	private void GenerateSineWaves() {
		if (_prevAmp != BaseAmplitude || _prevFreq != BaseFrequency || _prevPhaseMod != PhaseModifier || _prevPhase != BasePhase || _prevGain != Gain || _prevLac != Lacunarity) {
			var inputUniforms = new byte[32];
			
			float[] inputs = [BaseAmplitude, BaseFrequency, BasePhase, PhaseModifier, Lacunarity, Gain];
			Buffer.BlockCopy( inputs, 0, inputUniforms, 0, sizeof(float) * 6);
		
			ComputeShader.SetBuffer("paramBuffer", inputUniforms);
			_prevAmp = BaseAmplitude;
			_prevFreq = BaseFrequency;
			_prevPhase = BasePhase;
			_prevPhaseMod = PhaseModifier;
			_prevGain = Gain;
			_prevLac = Lacunarity;
		}
		
		if (UseBuffers) {
			bufferGen.Dispatch(WaveCount / 2, 1, 1, PushConstants());
			_material.SetShaderBufferRaw("waveBuffer", bufferGen.GetBufferData("waveBuffer"));
		} else {
			textureGen.Dispatch(WaveCount / 2, 1, 1, PushConstants());
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

	#region Helpers

	private void GenerateMesh() {
		if (UseBuffers) {
			_material = SumOfSinesMat;
			bufferGen.Dispatch(WaveCount / 2, 1, 1, PushConstants(false));
			_material.SetShaderBufferRaw("waveBuffer", bufferGen.GetBufferData("waveBuffer"));
		} else {
			_material = SumOfSinesTextureMat;
			_material.SetShaderParameter("waveCount", WaveCount);
			textureGen.Dispatch(WaveCount / 2, 1, 1, PushConstants(false));
		}
		Mesh = _plane;
		Mesh?.SurfaceSetMaterial(0, _material);
	}

	#endregion
}