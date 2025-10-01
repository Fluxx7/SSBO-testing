using Godot;
using System;
using GraphicsTesting.assets.Scripts.Utility;

[Tool]
public partial class ComputeController : Node2D
{
	private bool _simulate = true;

	[ExportGroup("JONSWAP Parameters")] 
	[Export] private float _windSpeed10 = 10.0f;
	[Export] private float _windSpeed19 = 10.0f;
	[Export] private Vector2 _windDirection;
	[Export] private float _fetch = 100.0f;
	[Export] private float _peakEnhancement = 3.3f;
	[Export] private float _depth = 10000.0f;
	[Export] private float _tileLength = 500.0f;
	[ExportGroup("")]
	[ExportToolButton("Regenerate Spectrum")]
	private Callable RegenWaves => Callable.From(RegenerateWaves);
	[ExportToolButton("Simulate")]
	private Callable Simulate => Callable.From(() => ToggleSimulate(!_simulate));
	[ExportToolButton("Regenerate Outputs")]
	private Callable UpdateWaves => Callable.From(GenerateWaves);
	[ExportToolButton("Clear Shaders")]
	private Callable ClearShaders => Callable.From(() => {
		_compHandler.Close();
		_compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		InitCompHandler();
	});
	[ExportToolButton("Reset Time")]
	private Callable ResetTime => Callable.From(() => {
		_time = 0.0f;
	});


	[Export] public TextureRect spectrumTexRect;
	[Export] public TextureRect displaceTexRect;
	[Export] public TextureRect gradientTexRect;
	[Export] public TextureRect normalTexRect;

	private ComputeHandler _compHandler = new();
	private float _currentSeed;
	private float _time;
	private uint _texSize = 128;
	private const int _updateRate = 32;
	private int _updateTimer = 0;

	public void ToggleSimulate(bool sim) {
		_simulate = sim;
	}

	public override void _Ready() {
		_compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		GenerateJonswap();
		GenerateWaves();
	}

	public override void _Process(double delta) {
		if (!IsOnScreen()) {
			return;
		}

		if (_simulate) {
			_time += (float)delta;
			if (_updateTimer >= _updateRate) {
				GenerateWaves();
				_updateTimer = 0;
			} else {
				_updateTimer++;
			}
		}
	}
	
	public override void _ExitTree() {
		_compHandler?.Close();
		base._ExitTree();
	}

	private void RegenerateWaves() {
		GenerateJonswap();
		GenerateWaves();
	}

	private void InitCompHandler() {
		if (!_compHandler.HasShader("jonswap_gen")) {
			_compHandler.AddShader("jonswap_gen", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/JONSWAP/jonswap_gen.glsl"));
			byte[] jonswapParams = new byte[32];
			Buffer.BlockCopy(new []{_windSpeed10, _windSpeed19, _windDirection.X, _windDirection.Y, _fetch, _peakEnhancement, _depth, _tileLength}, 0, jonswapParams, 0, 32);
			_compHandler.CreateBuffer("jonswapParams", RenderingDevice.UniformType.UniformBuffer, 32u, "jonswap_gen", 0, 0, jonswapParams);
			
			Texture2Drd gaussianTexture = new Texture2Drd();
			Texture2Drd spectrumTexture = new Texture2Drd();
			gaussianTexture.TextureRdRid = _compHandler.CreateTexture("gaussian_noise", _texSize, _texSize);
			_compHandler.AssignUniform("jonswap_gen", "gaussian_noise", 0, 1);
			spectrumTexture.TextureRdRid = _compHandler.CreateTexture("spectrumTexture", _texSize, _texSize);
			_compHandler.AssignUniform("jonswap_gen", "spectrumTexture", 1, 0);
			if (spectrumTexRect != null) {
				spectrumTexRect.Texture = spectrumTexture;
			}
		}

		if (_compHandler.HasShader("make_maps")) {
			return;
		}

		_compHandler.AddShader("make_maps", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/JONSWAP/tessendorf_idft_2d.glsl"));
		_compHandler.AssignUniform("make_maps", "spectrumTexture", 0, 0);
		Texture2Drd displacementTexture = new Texture2Drd();
		Texture2Drd gradientTexture = new Texture2Drd();
		displacementTexture.TextureRdRid = _compHandler.CreateTexture("displacementMap", _texSize, _texSize);
		_compHandler.AssignUniform("make_maps", "displacementMap", 1, 0);
		gradientTexture.TextureRdRid = _compHandler.CreateTexture("gradientMap", _texSize, _texSize);
		_compHandler.AssignUniform("make_maps", "gradientMap", 1, 1);
		Texture2Drd normalTexture = new Texture2Drd();
		normalTexture.TextureRdRid = _compHandler.CreateTexture("normalMap", _texSize, _texSize);
		_compHandler.AssignUniform("make_maps", "normalMap", 1, 2);
		if (displaceTexRect != null) {
			displaceTexRect.Texture = displacementTexture;
		}
		if (gradientTexRect != null) {
			gradientTexRect.Texture = gradientTexture;
		}
		if (normalTexRect != null) {
			normalTexRect.Texture = normalTexture;
		}
	}

	private void GenerateWaves() {
		if (!_compHandler.HasShader("jonswap_gen") || !_compHandler.HasUniform("spectrumTexture")) {
			GenerateJonswap();
		} else {
			InitCompHandler();
		}
		
		byte[] push_constants = new byte[16];
		Buffer.BlockCopy(new []{_texSize}, 0, push_constants, 0, sizeof(int));
		Buffer.BlockCopy(new []{_time, _tileLength, _depth}, 0, push_constants, sizeof(int), sizeof(float) * 3);

		_compHandler.Dispatch("make_maps", _texSize / 16,  _texSize / 16, 1, push_constants);
	}

	private bool IsOnScreen() {
		return true;
	}

	private void GenerateJonswap() {
		InitCompHandler();

		var rng = new RandomNumberGenerator();
		Image gaussian = Image.CreateEmpty((int)_texSize, (int)_texSize, false, Image.Format.Rgbah);
		for (int u = 0; u < _texSize; u++) {
			for (int v = 0; v < _texSize; v++) {
				Color new_pixel = new Color();
				new_pixel.R = rng.Randfn();
				new_pixel.G = rng.Randfn();
				new_pixel.A = 1f;
				gaussian.SetPixel(u,v, new_pixel);
			}
		}

		_compHandler.SetTexture("gaussian_noise", _texSize, _texSize, gaussian);
		
		byte[] push_constants = new byte[16];
		Buffer.BlockCopy(new []{_texSize}, 0, push_constants, 0, sizeof(int));
		
		_compHandler.Dispatch("jonswap_gen", _texSize / 16, _texSize / 16, 1, push_constants);
		
	}
}
