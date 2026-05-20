using Godot;
using System;
using GraphicsTesting.assets.Scripts.Utility;
using GraphicsTesting.Libraries.ComputeShaderHandling;

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


	[Export] public TextureRect spectrumTexRect;
	[Export] public TextureRect displaceTexRect;
	[Export] public TextureRect gradientTexRect;
	[Export] public TextureRect initialSpectrumTexRect;

	private ComputeShader jonswapGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/jonswap_gen_fixed.glsl");
	private TessendorfFFTHandler fftHandler;
	private float _currentSeed;
	private float _time;

	[Export]
	private uint texPower {
		get => _texPow;
		set {
			_texPow = value;
			_texSize = (uint) Math.Pow(2,value);
			if (fftHandler != null) {
				fftHandler.Resize(value);
				ComputeShader.SetTextureSize("baseSpectrumTexture", _texSize, _texSize);
				ComputeShader.SetTextureSize("spectrumTexture", _texSize, _texSize);
				ComputeShader.SetTextureSize("displacementMap", _texSize, _texSize);
				ComputeShader.SetTextureSize("gradientMap", _texSize, _texSize);
			}
			
			
		} 
	}

	private const uint starting_pow = 8;
	private uint _texPow = starting_pow;
	private uint _texSize = (uint) Math.Pow(2, starting_pow);

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
			GenerateWaves();
		}
	}
	
	public override void _ExitTree() {
		jonswapGen.Close();
		base._ExitTree();
	}

	private void RegenerateWaves() {
		GenerateJonswap();
		GenerateWaves();
	}

	private void InitShaders() {
		jonswapGen.CreateBuffer("jonswapParams", RenderingDevice.UniformType.UniformBuffer, 32u, 0, 0, 
			new ByteBuffer().Add([_windSpeed10, _windSpeed19, _windDirection.X, _windDirection.Y, _fetch, _peakEnhancement, _depth, _tileLength]).Generate());
		jonswapGen.CreateTexture("gaussian_noise", _texSize, _texSize, 0, 1);
		jonswapGen.CreateTexture("baseSpectrumTexture", _texSize, _texSize, 1, 0);
		ComputeShader.CreateTexture("spectrumTexture", _texSize, _texSize);
		ComputeShader.BindTextureParameter("baseSpectrumTexture", 
			Callable.From(
				(Texture2Drd baseSpectrum) => (initialSpectrumTexRect.Texture = baseSpectrum)));
		ComputeShader.BindTextureParameter("spectrumTexture", 
			Callable.From(
				(Texture2Drd spectrumTexture) => (spectrumTexRect.Texture = spectrumTexture)));
		
		ComputeShader.CreateTexture("displacementMap", _texSize, _texSize);
		ComputeShader.CreateTexture("gradientMap", _texSize, _texSize);
		ComputeShader.BindTextureParameter("displacementMap", 
			Callable.From(
				(Texture2Drd displacementTexture) => (displaceTexRect.Texture = displacementTexture)));
		ComputeShader.BindTextureParameter("gradientMap", 
			Callable.From(
				(Texture2Drd gradientTexture) => (gradientTexRect.Texture = gradientTexture)));
		
		fftHandler = new TessendorfFFTHandler(_texSize, "baseSpectrumTexture", "spectrumTexture", "displacementMap", "gradientMap");
	}

	private void GenerateWaves() {
		fftHandler.Run(_time, _tileLength, _depth);
	}
	
	private void GenerateJonswap() {
		var rng = new RandomNumberGenerator();
		Image gaussian = Image.CreateEmpty((int)_texSize, (int)_texSize, false, Image.Format.Rgbaf);
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
		
		
		jonswapGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize));
		
	}
}
