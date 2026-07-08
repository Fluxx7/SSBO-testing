// using Godot;
// using System;
// using GodotWaterRendering.assets.Scripts.Utility;
// using GodotWaterRendering.Libraries.ComputeShaderHandling;
//
// public partial class ComputeController : Node2D
// {
// 	
// 	enum SpectrumFunction {
// 		Tessendorf,
// 		TessendorfAB,
// 		JONSWAP,
// 		PiersonMoskowitz
// 	}
// 	
// 	enum DirectionalSpreadingFunction {
// 		None,
// 		Tessendorf
// 	}
// 	
// 	private bool _simulate = true;
// 	[Export] private float _tileLength = 500.0f;
// 	[Export] private SpectrumFunction spectrum = SpectrumFunction.JONSWAP;
// 	[Export] private DirectionalSpreadingFunction spread = DirectionalSpreadingFunction.Tessendorf;
// 	[Export] private float _depth = 500.0f;
// 	[ExportGroup("JONSWAP Parameters")] 
// 	[Export] private float _windSpeedJonswap = 10.0f;
// 	[Export] private float _windDirectionJonswap;
// 	[Export] private float _fetch = 100.0f;
// 	
// 	
// 	[ExportGroup("Philips Parameters")] 
// 	[Export] private float _windSpeedPhillips = 10.0f;
// 	[Export] private float _windDirectionPhilips;
// 	[Export] private float _philipsAmplitude = 0.000003f;
// 	
// 	[ExportGroup("Pierson Moskowitz Parameters")] 
// 	[Export] private float _windSpeedPierMosk = 10.0f;
// 	[Export] private float _windDirectionPierMosk;
//
// 	[ExportGroup("")]
// 	
//
// 	[Export] public TextureRect spectrumTexRect;
// 	[Export] public TextureRect displaceTexRect;
// 	[Export] public TextureRect gradientTexRect;
// 	[Export] public TextureRect foamTexRect;
// 	[Export] public TextureRect initialSpectrumTexRect;
//
// 	private ComputeShader jonswapGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/jonswap_gen.glsl");
// 	private ComputeShader tessendorfGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/tessendorf_spectrum_gen.glsl");
// 	private ComputeShader abSpectrumGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/a_b_spectrum_gen.glsl");
// 	private ComputeShader tessendorfSpreading = new("res://assets/Shaders/Compute/GLSL/Spreadings/tessendorf_spreading.glsl");
// 	private ComputeShader noSpreading = new("res://assets/Shaders/Compute/GLSL/Spreadings/no_spreading.glsl");
// 	private TessendorfFFTHandler fftHandler;
// 	private float _currentSeed;
// 	private float _time;
//
// 	[Export]
// 	private uint texPower {
// 		get => _texPow;
// 		set {
// 			_texPow = value;
// 			_texSize = (uint) Math.Pow(2,value);
// 			if (fftHandler != null) {
// 				fftHandler.Resize((int) value);
// 				ComputeShader.SetTextureSize("baseSpectrumTexture", _texSize, _texSize);
// 				ComputeShader.SetTextureSize("spectrumTexture", _texSize, _texSize);
// 				ComputeShader.SetTextureSize("displacementMap", _texSize, _texSize);
// 				ComputeShader.SetTextureSize("gradientMap", _texSize, _texSize);
// 			}
// 			
// 			
// 		} 
// 	}
//
// 	private const uint starting_pow = 8;
// 	private uint _texPow = starting_pow;
// 	private uint _texSize = (uint) Math.Pow(2, starting_pow);
// 	private const float g = 9.81f;
//
// 	public void ToggleSimulate(bool sim) {
// 		_simulate = sim;
// 	}
//
// 	public override void _Ready() {
// 		InitShaders();
// 		GenerateSpectrum();
// 		GenerateWaves(0.0f);
// 	}
//
// 	public override void _Process(double delta) {
// 		if (_simulate) {
// 			_time += (float)delta;
// 			GenerateWaves((float) delta);
// 		}
// 	}
// 	
// 	public override void _ExitTree() {
// 		jonswapGen.Close();
// 		base._ExitTree();
// 	}
//
// 	private void RegenerateWaves() {
// 		GenerateSpectrum();
// 		GenerateWaves(0.0f);
// 	}
//
// 	private void InitShaders() {
// 		ComputeShader.CreateTexture("gaussian_noise", _texSize, _texSize);
// 		ComputeShader.CreateTexture("baseSpectrumTexture", _texSize, _texSize);
// 		ComputeShader.CreateTexture("spectrumTexture", _texSize, _texSize);
// 		ComputeShader.BindTextureParameter("baseSpectrumTexture", 
// 			Callable.From(
// 				(Texture2Drd baseSpectrum) => (initialSpectrumTexRect.Texture = baseSpectrum)));
// 		ComputeShader.BindTextureParameter("spectrumTexture", 
// 			Callable.From(
// 				(Texture2Drd spectrumTexture) => (spectrumTexRect.Texture = spectrumTexture)));
// 		
// 		ComputeShader.CreateTexture("displacementMap", _texSize, _texSize);
// 		ComputeShader.CreateTexture("gradientMap", _texSize, _texSize);
// 		ComputeShader.BindTextureParameter("displacementMap", 
// 			Callable.From(
// 				(Texture2Drd displacementTexture) => (displaceTexRect.Texture = displacementTexture)));
// 		ComputeShader.BindTextureParameter("gradientMap", 
// 			Callable.From(
// 				(Texture2Drd gradientTexture) => (gradientTexRect.Texture = gradientTexture)));
// 		ComputeShader.CreateTexture("foamMap", _texSize, _texSize);
// 		ComputeShader.BindTextureParameter("foamMap", 
// 			Callable.From(
// 				(Texture2Drd foamTexture) => (foamTexRect.Texture = foamTexture)));
// 		InitSpectrums();
// 		InitSpreads();
// 		fftHandler = new TessendorfFFTHandler(_texSize, "baseSpectrumTexture", "spectrumTexture", "displacementMap", "gradientMap", "foamMap");
// 	}
//
// 	private void InitSpectrums() {
// 		tessendorfGen.AssignUniform("baseSpectrumTexture", 0, 0);
// 		
// 		abSpectrumGen.AssignUniform("baseSpectrumTexture", 0, 0);
// 		
// 		jonswapGen.AssignUniform("baseSpectrumTexture", 0, 0);
// 	}
//
// 	private void InitSpreads() {
// 		noSpreading.AssignUniform("baseSpectrumTexture", 0, 0);
// 		noSpreading.AssignUniform("gaussian_noise", 0, 1);
// 		
// 		tessendorfSpreading.AssignUniform("baseSpectrumTexture", 0, 0);
// 		tessendorfSpreading.AssignUniform("gaussian_noise", 0, 1);
// 	}
//
// 	private void DispatchTessendorf() {
// 		tessendorfGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize).Add([_windSpeedPhillips, _philipsAmplitude, _tileLength]));
// 	}
// 	
// 	private void DispatchABSpectrum(float a, float b) {
// 		abSpectrumGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize).Add([_tileLength, a, b]));
// 	}
// 	
// 	private void DispatchJonswap() {
// 		jonswapGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize).Add([_windSpeedJonswap, _fetch, _tileLength]));
// 	}
// 	
// 	private void DispatchNoSpread(float windDirX, float windDirY) {
// 		noSpreading.Dispatch( _texSize / 16, _texSize / 16, 1, 
// 			new ByteBuffer(_texSize).Add([_tileLength, windDirX, windDirY]));
// 	}
// 	
// 	private void DispatchTessendorfSpread(float windDirX, float windDirY) {
// 		tessendorfSpreading.Dispatch( _texSize / 16, _texSize / 16, 1, 
// 			new ByteBuffer(_texSize).Add([_tileLength, windDirX, windDirY]));
// 	}
// 	
// 	private void GenerateWaves(float delta) {
// 		fftHandler.Run(delta, _time, _tileLength, _depth);
// 	}
// 	
// 	private void GenerateSpectrum() {
// 		var rng = new RandomNumberGenerator();
// 		Image gaussian = Image.CreateEmpty((int)_texSize, (int)_texSize, false, Image.Format.Rgbaf);
// 		for (int u = 0; u < _texSize; u++) {
// 			for (int v = 0; v < _texSize; v++) {
// 				Color new_pixel = new Color();
// 				new_pixel.R = rng.Randfn();
// 				new_pixel.G = rng.Randfn();
// 				new_pixel.A = 1f;
// 				gaussian.SetPixel(u,v, new_pixel);
// 			}
// 		}
//
// 		ComputeShader.SetTexture("gaussian_noise", _texSize, _texSize, gaussian);
// 		float windDir = 0;
// 		var a = 0f;
// 		var b = 0f;
// 		switch (spectrum) {
// 			case SpectrumFunction.JONSWAP:
// 				windDir = float.DegreesToRadians(_windDirectionJonswap);
// 				DispatchJonswap();
// 				break;
// 			case SpectrumFunction.Tessendorf:
// 				windDir = float.DegreesToRadians(_windDirectionPhilips);
// 				DispatchTessendorf();
// 				break;
// 			case SpectrumFunction.TessendorfAB:
// 				windDir = float.DegreesToRadians(_windDirectionPhilips);
// 				a = 2.0f * _philipsAmplitude * g * g;
// 				b = float.Pow(g / _windSpeedPierMosk, 4.0f);
// 				DispatchABSpectrum(a, b);
// 				break;
// 			case SpectrumFunction.PiersonMoskowitz:
// 				windDir = float.DegreesToRadians(_windDirectionPierMosk);
// 				a = 8.1e-3f * g * g;
// 				b = 0.6858f * float.Pow(g / _windSpeedPierMosk, 4.0f);
// 				DispatchABSpectrum(a, b);
// 				break;
// 		}
// 		
// 		float windDirX = float.Cos(windDir);
// 		float windDirY = float.Sin(windDir);
// 		
// 		switch (spread) {
// 			case DirectionalSpreadingFunction.None:
// 				DispatchNoSpread(windDirX, windDirY);
// 				break;
// 			case DirectionalSpreadingFunction.Tessendorf:
// 				DispatchTessendorfSpread(windDirX, windDirY);
// 				break;
// 		}
// 		
// 		
// 	}
// }
