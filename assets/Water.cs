using System;
using Godot;
using Godot.Collections;
using Godot.NativeInterop;

using Wave = Godot.Collections.Dictionary<Godot.StringName, float>;
[Tool]
public partial class Water : MeshInstance3D {
	[Export] public float Time = 0.0f;


	public enum WaterSystem {
		SumOfSines,
		JONSWAP
	};

	[Export] public WaterSystem _waterSystem = WaterSystem.SumOfSines;


	private ShaderMaterial _material;
	[ExportGroup("Materials")] [Export] public ShaderMaterial _sumOfSinesMat = null;
	[Export] public ShaderMaterial _JONSWAPWaterMat = null; // not implemented yet
	[ExportGroup("")] private PlaneMesh plane = null;
	[Export] public Vector2 size;
	[Export] public int subdivide;

	[Export] public bool useBuffers = true;
	[Export] public bool render = true;
	[Export] public bool simulate = false;

	[ExportGroup("Sum of Sines")]
	//[Export] public uint waveCount = 16;
	[ExportSubgroup("Wave Parameters")]
	[Export]
	public float baseAmplitude = 1.0f;

	[Export] public float baseFrequency = 0.1f;
	[Export] public float gain = 1.18f;
	[Export] public float lacunarity = 0.82f;
	[Export] public float phase_modifier = 1.0f;

	public enum TextureSize {
		t256 = 256,
		t512 = 512,
		t1024 = 1024,
	}

	public struct SpectrumParameters { }


	private Array<Wave> waves;
	[ExportGroup("JONSWAP")] [Export] public TextureSize texSize = TextureSize.t1024;
	[Export] public uint numTextures = 4;

	[ExportGroup("")]
	[ExportToolButton("Regenerate Mesh")]
	public Callable regenerate => Callable.From(RegenerateMesh);
	[ExportToolButton("Regenerate Waves")]
	public Callable regenWaves => Callable.From(() => { waves = GenerateSineWaves(); RegenerateMesh();});


	#region Overrides

	public override void _Ready() {
		if (Engine.IsEditorHint()) return;
		waves = GenerateSineWaves();
		GenerateMesh();
	}

	public override void _Process(double delta) {
		if (Engine.IsEditorHint()) {
			if (plane != null && !render) {
				plane = null;
				Mesh = null;
			} else if (plane == null && render) {
				GenerateMesh();
			}

			_material.SetShaderParameter("size", size);

			if (!simulate) {
				return;
			}
		}

		Time += (float)delta;
		_material.SetShaderParameter("time", Time);
	}

	#endregion

	private Array<Wave> GenerateSineWaves() {
		var new_waves = new Array<Wave>();
		new_waves.Resize(42);
		var rng = new RandomNumberGenerator();
		float seed = 0.0f;
		float seedMod = rng.RandfRange(0f, 4f * Mathf.Pi);
		float frequency = baseFrequency;
		float amplitude = baseAmplitude;
		float phase = phase_modifier;
		for (int i = 0; i < 42; i++) {
			var wave = new Wave();
			wave["directionX"] = Mathf.Sin(seed);
			wave["directionY"] = Mathf.Cos(seed);
			wave["amplitude"] = amplitude;
			wave["frequency"] = frequency;
			wave["phase"] = phase;
			new_waves[i] = wave;

			seed += seedMod; //
			frequency *= gain;
			phase *= 1.07f;
			amplitude = Mathf.Lerp(amplitude, 0.0f, (1f - lacunarity)); // lacunarity;// * Mathf.Pow(lacunarity, i);
		}

		return new_waves;
	}

	private ImageTexture GenerateJONSWAP() {
		var image = Godot.Image.CreateEmpty((int)42, (int)42, false, Image.Format.Rgbaf);
		var rng = new RandomNumberGenerator();

		ImageTexture tex = ImageTexture.CreateFromImage(image);
		return tex;
	}


	#region Helpers

	public void RegenerateMesh() {
		plane = null;
		GenerateMesh();
	}

	private void UpdateShaderWaves(Array<Wave> waves) {
		if (useBuffers) {
			_material.SetShaderBuffer("waveBuffer", 
				new Dictionary<StringName, Variant> { 
					{"wave_array", waves} 
				});
		} else {
			
		}
	}

	private void GenerateMesh() {
		if (plane != null) return;
		switch (_waterSystem) {
			case WaterSystem.SumOfSines:
				_material = _sumOfSinesMat;
				UpdateShaderWaves(waves);
				break;
			case WaterSystem.JONSWAP:
				_material = _JONSWAPWaterMat;
				break;
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