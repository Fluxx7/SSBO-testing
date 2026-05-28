using Godot;
using System;
using System.Collections.Generic;
using GraphicsTesting.assets.Scripts.Utility;
using GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class WaterController : Node {

	enum SpectrumFunction {
		Tessendorf,
		PiersonMoskowitz,
		JONSWAP,
		TMA
	}

	enum DirectionalSpreadingFunction {
		None,
		Tessendorf
	}

	private bool _simulate = true;
	private float _tileLength = 256.0f;

	[Export] public float TileLength {
		get => _tileLength;
		set {
			_tileLength = value;
			RenderingServer.GlobalShaderParameterSet("tileLength", value);
			if (fftHandler != null) {
				GenerateSpectrum();
				GenerateWaves(0.0f);
			}
		}
	}
	[Export] private SpectrumFunction spectrum = SpectrumFunction.JONSWAP;
	[Export] private DirectionalSpreadingFunction spread = DirectionalSpreadingFunction.Tessendorf;
	[Export] private float _depth = 1000.0f;
	[Export] private float _windSpeed = 10.0f;
	[Export] private float _windDirection;
	[Export] private float _fetch = 10000.0f;
	[Export] private float _philipsAmplitude = 0.0008f;
	[ExportGroup("")]
	

	[Export] public TextureRect spectrumTexRect;
	[Export] public TextureRect displaceTexRect;
	[Export] public TextureRect gradientTexRect;
	[Export] public TextureRect foamTexRect;
	[Export] public TextureRect initialSpectrumTexRect;
	[Export] public TextureRect normalTexRect;

	private ComputeShader jonswapGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/jonswap_gen.glsl");
	private ComputeShader tessendorfGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/tessendorf_spectrum_gen.glsl");
	private ComputeShader abSpectrumGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/a_b_spectrum_gen.glsl");
	private ComputeShader tmaSpectrumGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/tma_spectrum_gen.glsl");
	private ComputeShader tessendorfSpreading = new("res://assets/Shaders/Compute/GLSL/Spreadings/tessendorf_spreading.glsl");
	private ComputeShader noSpreading = new("res://assets/Shaders/Compute/GLSL/Spreadings/no_spreading.glsl");
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
	private const float g = 9.81f;

	private Dictionary<StringName, uint> shaderUniforms = new();

	public void UpdateProperty(Variant value, StringName property) {
		switch (property) {
			case "Depth":
				_depth = value.AsSingle();
				GenerateSpectrum();
				GenerateWaves(0.0f);
				break;
			case "Fetch":
				_fetch = value.AsSingle();
				GenerateSpectrum();
				GenerateWaves(0.0f);
				break;
			case "WindSpeed":
				_windSpeed = value.AsSingle();
				GenerateSpectrum();
				GenerateWaves(0.0f);
				break;
			case "WindDirection":
				_windDirection = value.AsSingle();
				GenerateSpectrum();
				GenerateWaves(0.0f);
				break;
			case "TileLength":
				TileLength = value.AsSingle();
				break;
			case "Spectrum":
				switch (value.AsInt32()) {
					case 0:
						spectrum = SpectrumFunction.Tessendorf;
						break;
					case 1:
						spectrum = SpectrumFunction.PiersonMoskowitz;
						break;
					case 2:
						spectrum = SpectrumFunction.JONSWAP;
						break;
					case 3:
						spectrum = SpectrumFunction.TMA;
						break;
				}
				GenerateSpectrum();
				GenerateWaves(0.0f);
				break;
			case "Spread":
				switch (value.AsInt32()) {
					case 0:
						spread = DirectionalSpreadingFunction.None;
						break;
					case 1:
						spread = DirectionalSpreadingFunction.Tessendorf;
						break;
				}
				GenerateSpectrum();
				GenerateWaves(0.0f);
				break;
		}
	}
	
	public void ToggleSimulate(bool sim) {
		_simulate = sim;
	}

	public override void _Ready() {
		InitShaders();
		GenerateSpectrum();
		GenerateWaves(0.0f);
		RenderingServer.GlobalShaderParameterSet("tileLength", _tileLength);
	}

	public override void _Process(double delta) {
		if (_simulate) {
			_time += (float)delta;
			GenerateWaves((float) delta);
		}
	}
	
	public override void _ExitTree() {
		jonswapGen.Close();
		base._ExitTree();
	}

	private void RegenerateWaves() {
		GenerateGaussian();
		GenerateSpectrum();
		GenerateWaves(0.0f);
	}

	private void InitShaders() {
		uint gaussian_noise_id = ComputeShader.CreateInternalTexture(_texSize, _texSize);
		uint base_spectrum_tex_id = ComputeShader.CreateInternalTexture(_texSize, _texSize);
		uint spectrum_tex_id = ComputeShader.CreateInternalTexture(_texSize, _texSize);
		uint displacement_tex_id = ComputeShader.CreateInternalTexture(_texSize, _texSize);
		uint gradient_tex_id = ComputeShader.CreateInternalTexture(_texSize, _texSize);
		uint foam_tex_id = ComputeShader.CreateInternalTexture(_texSize, _texSize);
		uint normal_tex_id = ComputeShader.CreateInternalTexture(_texSize, _texSize);
		
		ComputeShader.BindInternalTextureParameter(base_spectrum_tex_id, 
			Callable.From(
				(Texture2Drd baseSpectrum) => (initialSpectrumTexRect.Texture = baseSpectrum)));
		ComputeShader.BindInternalTextureParameter(spectrum_tex_id, 
			Callable.From(
				(Texture2Drd spectrumTexture) => (spectrumTexRect.Texture = spectrumTexture)));
		ComputeShader.BindInternalTextureParameter(normal_tex_id, 
			Callable.From(
				(Texture2Drd normalTexture) => {
					RenderingServer.GlobalShaderParameterSet("normalMap", normalTexture);
					return normalTexRect.Texture = normalTexture;
				}));
		
		ComputeShader.BindInternalTextureParameter(displacement_tex_id, 
			Callable.From(
				(Texture2Drd displacementTexture) => {
					RenderingServer.GlobalShaderParameterSet("displacementMap", displacementTexture);
					return displaceTexRect.Texture = displacementTexture;
				}));
		ComputeShader.BindInternalTextureParameter(gradient_tex_id, 
			Callable.From(
				(Texture2Drd gradientTexture) => {
					RenderingServer.GlobalShaderParameterSet("gradientMap", gradientTexture);
					return gradientTexRect.Texture = gradientTexture;
				}));
		ComputeShader.BindInternalTextureParameter(foam_tex_id, 
			Callable.From(
				(Texture2Drd foamTexture) => {
					RenderingServer.GlobalShaderParameterSet("foamMap", foamTexture);
					return foamTexRect.Texture = foamTexture;
				}));
		
		shaderUniforms.Add("gaussian_noise", gaussian_noise_id);
		shaderUniforms.Add("baseSpectrumTexture", base_spectrum_tex_id);
		shaderUniforms.Add("spectrumTexture", spectrum_tex_id);
		shaderUniforms.Add("displacementMap", displacement_tex_id);
		shaderUniforms.Add("gradientMap", gradient_tex_id);
		shaderUniforms.Add("foamMap", foam_tex_id);
		GenerateGaussian();
		InitSpectrums();
		InitSpreads();
		fftHandler = new TessendorfFFTHandler(_texSize, base_spectrum_tex_id, spectrum_tex_id, displacement_tex_id, gradient_tex_id, foam_tex_id, normal_tex_id);
	}

	private void InitSpectrums() {
		tessendorfGen.AssignUniform(shaderUniforms["baseSpectrumTexture"], 0, 0);
		
		abSpectrumGen.AssignUniform(shaderUniforms["baseSpectrumTexture"], 0, 0);
		
		jonswapGen.AssignUniform(shaderUniforms["baseSpectrumTexture"], 0, 0);
		
		tmaSpectrumGen.AssignUniform(shaderUniforms["baseSpectrumTexture"], 0, 0);
	}

	private void InitSpreads() {
		noSpreading.AssignUniform(shaderUniforms["baseSpectrumTexture"], 0, 0);
		noSpreading.AssignUniform(shaderUniforms["gaussian_noise"], 0, 1);
		
		tessendorfSpreading.AssignUniform(shaderUniforms["baseSpectrumTexture"], 0, 0);
		tessendorfSpreading.AssignUniform(shaderUniforms["gaussian_noise"], 0, 1);
	}

	private void DispatchTessendorf() {
		tessendorfGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize).Add([_windSpeed, _philipsAmplitude, _tileLength]));
	}
	
	private void DispatchABSpectrum(float a, float b) {
		abSpectrumGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize).Add([_tileLength, a, b, _depth]));
	}
	
	private void DispatchJonswap() {
		jonswapGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize).Add([_windSpeed, _fetch, _depth, _tileLength]));
	}
	
	private void DispatchTMA() {
		tmaSpectrumGen.Dispatch( _texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize).Add([_windSpeed, _fetch, _depth, _tileLength]));
	}
	
	private void DispatchNoSpread() {
		noSpreading.Dispatch( _texSize / 16, _texSize / 16, 1, 
			new ByteBuffer(_texSize).Add([_tileLength, float.DegreesToRadians(_windDirection)]));
	}
	
	private void DispatchTessendorfSpread() {
		tessendorfSpreading.Dispatch( _texSize / 16, _texSize / 16, 1, 
			new ByteBuffer(_texSize).Add([_tileLength, float.DegreesToRadians(_windDirection)]));
	}
	
	private void GenerateWaves(float delta) {
		
		fftHandler.Run(delta, _time, _tileLength, _depth);
	}

	private void GenerateGaussian() {
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

		ComputeShader.SetInternalTexture(shaderUniforms["gaussian_noise"], _texSize, _texSize, gaussian);
	}

	private void GenerateSpectrum() {
		switch (spectrum) {
			case SpectrumFunction.JONSWAP:
				DispatchJonswap();
				break;
			case SpectrumFunction.Tessendorf:
				DispatchTessendorf();
				break;
			case SpectrumFunction.PiersonMoskowitz:
				float a = 8.1e-3f * g * g;
				float b = 0.6858f * float.Pow(g / _windSpeed, 4.0f);
				DispatchABSpectrum(a, b);
				break;
			case SpectrumFunction.TMA:
				DispatchTMA();
				break;
		}
		
		switch (spread) {
			case DirectionalSpreadingFunction.None:
				DispatchNoSpread();
				break;
			case DirectionalSpreadingFunction.Tessendorf:
				DispatchTessendorfSpread();
				break;
		}
		
		
	}
}
