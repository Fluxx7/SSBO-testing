using Godot;
using System;
using Godot.Collections;
using FluxxiShaderLang;
using GodotWaterRendering.assets.Scripts.Utility;

public partial class WaterController : Node {

	enum SpectrumFunction {
		Tessendorf,
		PiersonMoskowitz,
		Jonswap,
		Tma
	}

	enum DirectionalSpreadingFunction {
		None,
		Tessendorf
	}

	private bool _simulate = true;
	[Export] private SpectrumFunction spectrum = SpectrumFunction.Jonswap;
	[Export] private DirectionalSpreadingFunction spread = DirectionalSpreadingFunction.Tessendorf;
	[Export] private float _depth = 1000.0f;
	[Export] private float _windSpeed = 10.0f;
	[Export] private float _windDirection;
	[Export] private float _fetch = 10000.0f;
	[Export] private float _philipsAmplitude = 0.02f;
	[ExportGroup("")] 
	
	[Export] private Window controlWindow;
	
	private FSLFile spectrumShader = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/spectrums.fsl");
	private FSLFile spreadingsShader = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/spreadings.fsl");
	private FSLFile bufferUpdateShader = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/temp.fsl");
	private ComputeGroup spectrums;
	private ComputeGroup spreadings;
	private ComputeGroup bufferUpdaters;
	private TessendorfFFTHandler tessFftHandler;
	private float _currentSeed;
	private float _time;
	[Export] public uint numCascades = 2;
	
	private Array<float> _tileLengths = new(){500f, 30f};
	[Export] public Array<float> TileLengths {
		get => _tileLengths;
		set {
			_tileLengths = value;
			oceanParams?.SetField("tileLengths", value);
			if (tessFftHandler != null) {
				GenerateSpectrum();
				GenerateWaves(0.0f);
			}
		}
	}
	
	[Signal]
	public delegate void TextureRidUpdatedEventHandler(StringName texture_name, Texture2DArrayRD new_tex_rd);

	[Export]
	private uint TexPower {
		get => _texPow;
		set {
			_texPow = value;
			_texSize = (uint) Math.Pow(2,value);
			tessFftHandler?.Resize(_texSize);
		} 
	}

	private const int StartingPow = 8;
	private uint _texPow = StartingPow;
	private uint _texSize = (uint) Math.Pow(2, StartingPow);
	private const float G = 9.81f;

	private FSLTexture gaussianNoise;
	private FSLBuffer oceanParams;

	private Dictionary<StringName, Texture2DArrayRD> texRdCache = new();

	private Action<Rid> MakeTextureCallback(StringName texture_name) {
		return tex_rid => {
			Texture2DArrayRD texRd = new Texture2DArrayRD();
			texRd.TextureRdRid = tex_rid;
			EmitSignal(SignalName.TextureRidUpdated, texture_name, texRd);
			texRdCache[texture_name] = texRd;
		};
	}

	public void PostTextures() {
		foreach (var (texName, texRd) in texRdCache) {
			EmitSignalTextureRidUpdated(texName, texRd);
		}
	}

	// public void UpdateProperty(Variant value, StringName property) {
	// 	switch (property) {
	// 		case "Depth":
	// 			_depth = value.AsSingle();
	// 			GenerateSpectrum();
	// 			GenerateWaves(0.0f);
	// 			break;
	// 		case "Fetch":
	// 			_fetch = value.AsSingle();
	// 			GenerateSpectrum();
	// 			GenerateWaves(0.0f);
	// 			break;
	// 		case "WindSpeed":
	// 			_windSpeed = value.AsSingle();
	// 			GenerateSpectrum();
	// 			GenerateWaves(0.0f);
	// 			break;
	// 		case "WindDirection":
	// 			_windDirection = value.AsSingle();
	// 			GenerateSpectrum();
	// 			GenerateWaves(0.0f);
	// 			break;
	// 		// case "TileLength":
	// 		// 	TileLength = value.AsSingle();
	// 		// 	break;
	// 		case "Spectrum":
	// 			switch (value.AsInt32()) {
	// 				case 0:
	// 					spectrum = SpectrumFunction.Tessendorf;
	// 					break;
	// 				case 1:
	// 					spectrum = SpectrumFunction.PiersonMoskowitz;
	// 					break;
	// 				case 2:
	// 					spectrum = SpectrumFunction.JONSWAP;
	// 					break;
	// 				case 3:
	// 					spectrum = SpectrumFunction.TMA;
	// 					break;
	// 			}
	// 			GenerateSpectrum();
	// 			GenerateWaves(0.0f);
	// 			break;
	// 		case "Spread":
	// 			switch (value.AsInt32()) {
	// 				case 0:
	// 					spread = DirectionalSpreadingFunction.None;
	// 					break;
	// 				case 1:
	// 					spread = DirectionalSpreadingFunction.Tessendorf;
	// 					break;
	// 			}
	// 			GenerateSpectrum();
	// 			GenerateWaves(0.0f);
	// 			break;
	// 	}
	// }
	
	public void ToggleSimulate(bool sim) {
		_simulate = sim;
	}

	public override void _Ready() {
		InitShaders();
		GenerateSpectrum();
		GenerateWaves(0.0f);
	}

	public override void _Process(double delta) {
		if (_simulate) {
			_time += (float)delta;
			GenerateWaves((float) delta);
		}
	}

	private void RegenerateWaves() {
		GenerateGaussian();
		GenerateSpectrum();
		GenerateWaves(0.0f);
	}

	private void InitShaders() {
		spectrums = spectrumShader.GetKernelGroup();
		spreadings = spreadingsShader.GetKernelGroup();
		bufferUpdaters = bufferUpdateShader.GetKernelGroup();
		
		FSLTexture spectrumMap = spectrums.GetTexture("spectrumMap");
		spectrumMap.Set3DTexture(_texSize, _texSize, uint.Max(numCascades, 2));
		FSLTexture baseSpectrum = spreadings.GetTexture("baseSpectrum");
		baseSpectrum.Set3DTexture(_texSize, _texSize, uint.Max(numCascades, 2));
		gaussianNoise = spreadings.GetTexture("spectrumCoefficients");
		oceanParams = bufferUpdaters.GetBuffer("oceanParams");
		oceanParams.SetUnsizedElementCount((uint) _tileLengths.Count);
		bufferUpdaters.Dispatch("updateParams", 1, 1, 1, new Dictionary<StringName, Variant> {
			{"numCascades", numCascades},
			{"windSpeed", _windSpeed},
			{"windDirection", _windDirection},
			{"depth", _depth},
			{"fetch", _fetch}
		});
		for (var tl_index = 0; tl_index < _tileLengths.Count; tl_index++) {
			bufferUpdaters.Dispatch("updateTileLength", 1, 1, 1, new Dictionary<StringName, Variant> {
				{"tileLengthIndex", (uint) tl_index},
				{"tileLength", _tileLengths[tl_index]}
			});
		}
		// oceanParams.SetBuffer(new Dictionary<StringName, Variant> {
		// 	{"numCascades", numCascades},
		// 	{"windSpeed", _windSpeed},
		// 	{"windDirection", _windDirection},
		// 	{"depth", _depth},
		// 	{"fetch", _fetch},
		// 	{"tileLengths", _tileLengths}
		// });
		
		spectrums.AssignResource(oceanParams, "oceanParams");
		spreadings.AssignResource(spectrumMap, "spectrumMap");
		spreadings.AssignResource(oceanParams, "oceanParams");
		
		baseSpectrum.BindCallback(Callable.From(MakeTextureCallback("baseSpectrum")));

		GenerateGaussian();
		TessendorfFFTHandler.TextureCallbacks callbacks = new TessendorfFFTHandler.TextureCallbacks{
			spectrumCallback = MakeTextureCallback("spectrumTexture"),
			heightCallback = MakeTextureCallback("heightMaps"),
			gradientCallback = MakeTextureCallback("gradientMaps"),
			foamCallback = MakeTextureCallback("foamMaps"),
			normalCallback = MakeTextureCallback("normalMaps")
		};
		tessFftHandler = new TessendorfFFTHandler(_texSize, numCascades, baseSpectrum, oceanParams, callbacks);
	}
	
	private void GenerateWaves(float delta) {
		
		tessFftHandler.Run(delta, _time, _tileLengths[0], _depth);
	}

	private void GenerateGaussian() {
		
		var rng = new RandomNumberGenerator();
		Array<Image> gaussians = new();
		for (int cascade = 0; cascade < (int)numCascades; cascade++) {
			gaussians.Add(Image.CreateEmpty((int) _texSize, (int) _texSize, false, Image.Format.Rgbah));
		}
		for (int u = 0; u < _texSize; u++) {
			for (int v = 0; v < _texSize; v++) {
				for (int cascade = 0; cascade < (int)numCascades; cascade++) {
					Color newPixel = new Color();
					newPixel.R = rng.Randfn();
					newPixel.G = rng.Randfn();
					newPixel.A = 1f;
					gaussians[cascade].SetPixel(u,v, newPixel);
				}
			}
		}
		

		gaussianNoise.Set3DTexture(_texSize, _texSize, numCascades, gaussians);
	}

	private void GenerateSpectrum() {
		Dictionary<StringName, Variant> pushConstants = new(){
			{"texSize", (int) _texSize}
		};
		StringName spectrumKernelName = "";
		switch (spectrum) {
			case SpectrumFunction.Jonswap:
				spectrumKernelName = "jonswapSpectrum";
				break;
			case SpectrumFunction.Tessendorf:
				spectrumKernelName = "tessendorfSpectrum";
				pushConstants.Add("A", _philipsAmplitude);
				break;
			case SpectrumFunction.PiersonMoskowitz:
				pushConstants.Add("A", 8.1e-3f * G * G);
				pushConstants.Add("B", 0.6858f * float.Pow(G / _windSpeed, 4.0f));
				spectrumKernelName = "abSpectrum";
				break;
			case SpectrumFunction.Tma:
				spectrumKernelName = "tmaSpectrum";
				break;
		}
		spectrums.Dispatch(spectrumKernelName, _texSize, _texSize, numCascades, pushConstants);
		
		StringName spreadingKernelName = "";
		pushConstants = new(){
			{"texSize", (int) _texSize}
		};
		
		switch (spread) {
			case DirectionalSpreadingFunction.None:
				spreadingKernelName = "noSpreading";
				break;
			case DirectionalSpreadingFunction.Tessendorf:
				spreadingKernelName = "positiveCosSpreading";
				break;
		}
		spreadings.Dispatch(spreadingKernelName, _texSize, _texSize, numCascades, pushConstants);
	}

	private void InitControlWindow() {
		
	}
}
