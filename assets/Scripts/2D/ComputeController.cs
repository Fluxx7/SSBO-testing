using Godot;
using System;
using GraphicsTesting.assets.Scripts.Utility;
using GraphicsTesting.Libraries.ComputeShaderHandling;

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
	[ExportToolButton("Reset Time")]
	private Callable ResetTime => Callable.From(() => {
		_time = 0.0f;
	});


	[Export] public TextureRect spectrumTexRect;
	[Export] public TextureRect displaceTexRect;
	[Export] public TextureRect gradientTexRect;
	[Export] public TextureRect initialSpectrumTexRect;

	private ComputeShader jonswapGen = new("res://assets/Shaders/Compute/GLSL/JONSWAP/jonswap_gen.glsl");
	private ComputeShader updateSpectrum = new("res://assets/Shaders/Compute/GLSL/JONSWAP/update_spectrum.glsl");
	private ComputeShader mapGen = new("res://assets/Shaders/Compute/GLSL/JONSWAP/tessendorf_idft_2d.glsl");
	private float _currentSeed;
	private float _time;
	[Export] private uint _texSize = 64;
	private const int _updateRate = 32;
	private int _updateTimer = 0;

	public void ToggleSimulate(bool sim) {
		_simulate = sim;
	}

	public override void _Ready() {
		InitShaders();
		GenerateJonswap();
		GenerateWaves();
	}

	public override void _Process(double delta) {
		if (_simulate) {
			_time += (float)delta;
			if (_updateTimer >= _updateRate) {
				if (_texSize <= 256) {
					GenerateWaves();
				}
				_updateTimer = 0;
			} else {
				_updateTimer++;
			}
		}
	}
	
	public override void _ExitTree() {
		base._ExitTree();
	}

	private void RegenerateWaves() {
		GenerateJonswap();
		GenerateWaves();
	}

	private void InitShaders() {
		byte[] jonswapParams = new byte[32];
		Buffer.BlockCopy(new []{_windSpeed10, _windSpeed19, _windDirection.X, _windDirection.Y, _fetch, _peakEnhancement, _depth, _tileLength}, 0, jonswapParams, 0, 32);
		jonswapGen.CreateBuffer("jonswapParams", RenderingDevice.UniformType.UniformBuffer, 32u, 0, 0, jonswapParams);
		jonswapGen.CreateTexture("gaussian_noise", _texSize, _texSize, 0, 1);
		jonswapGen.CreateTexture("baseSpectrumTexture", _texSize, _texSize, 1, 0);
		jonswapGen.BindTextureParameter("baseSpectrumTexture", 
			Callable.From(
				(Texture2Drd baseSpectrum) => (initialSpectrumTexRect.Texture = baseSpectrum)));

		updateSpectrum.AssignUniform("baseSpectrumTexture", 0, 0);
		updateSpectrum.CreateTexture("spectrumTexture", _texSize, _texSize, 1, 0);
		updateSpectrum.BindTextureParameter("spectrumTexture", 
			Callable.From(
				(Texture2Drd spectrumTexture) => (spectrumTexRect.Texture = spectrumTexture)));
		
		mapGen.AssignUniform("spectrumTexture", 0, 0);
		mapGen.CreateTexture("displacementMap", _texSize, _texSize, 1, 0);
		mapGen.CreateTexture("gradientMap", _texSize, _texSize, 1, 1);
		mapGen.BindTextureParameter("displacementMap", 
			Callable.From(
				(Texture2Drd displacementTexture) => (displaceTexRect.Texture = displacementTexture)));
		mapGen.BindTextureParameter("gradientMap", 
			Callable.From(
				(Texture2Drd gradientTexture) => (gradientTexRect.Texture = gradientTexture)));
	}

	private void GenerateWaves() {
		byte[] push_constants = new byte[16];
		Buffer.BlockCopy(new []{_texSize}, 0, push_constants, 0, sizeof(int));
		Buffer.BlockCopy(new []{_time, _tileLength, _depth}, 0, push_constants, sizeof(int), sizeof(float) * 3);

		updateSpectrum.Dispatch(_texSize / 16,  _texSize / 16, 1, push_constants);
		mapGen.Dispatch(_texSize / 16,  _texSize / 16, 1, push_constants);
	}
	
	private void GenerateJonswap() {
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

		ComputeShader.SetTexture("gaussian_noise", _texSize, _texSize, gaussian);
		
		byte[] push_constants = new byte[16];
		Buffer.BlockCopy(new []{_texSize}, 0, push_constants, 0, sizeof(int));
		
		jonswapGen.Dispatch( _texSize / 16, _texSize / 16, 1, push_constants);
		
	}
}
