using System;
using Godot;
using Godot.Collections;
using Godot.NativeInterop;

[Tool]
public partial class Water : MeshInstance3D
{
	[Export] public float Time = 0.0f;



	public enum WaterSystem
	{
		SumOfSines,
		JONSWAP
	};
	[Export] public WaterSystem _waterSystem = WaterSystem.SumOfSines;


	private ShaderMaterial _material;
	[ExportGroup("Materials")]
	[Export] public ShaderMaterial _sumOfSinesMat = null;
	[Export] public ShaderMaterial _JONSWAPWaterMat = null; // not implemented yet
	[ExportGroup("")]

	private PlaneMesh plane = null;
	[Export] public Vector2 size;
	[Export] public int subdivide;
	

	[Export] public bool render = true;
	[Export] public bool simulate = false;

	[ExportGroup("Sum of Sines")]
	//[Export] public uint waveCount = 16;
	[ExportSubgroup("Wave Parameters")]
	
	[Export] public float baseAmplitude = 1.0f;
	[Export] public float baseFrequency = 0.1f;
	[Export] public float gain = 1.18f;
	[Export] public float lacunarity = 0.82f;
	[Export] public float phase_modifier = 1.0f;
	
	public enum TextureSize
	{
		t256 = 256,
		t512 = 512,
		t1024 = 1024,
	}

	public struct SpectrumParameters
	{
		
	}

	public struct Wave
	{
		public Vector2 direction { set; get; }
		public float amplitude { set; get; }
		public float frequency {set; get; }
		public float phase {set; get; }
	}

	[ExportGroup("JONSWAP")]
	[Export] public TextureSize texSize = TextureSize.t1024;
	[Export] public uint numTextures = 4;
	[ExportGroup("")]
	[ExportToolButton("Regenerate Mesh")]
	public Callable regenerate => Callable.From(RegenerateMesh);
	

	

	#region Overrides
	
	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		GenerateMesh();
	}
	
	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			if (plane != null && !render)
			{
				plane = null;
				Mesh = null;
			}
			else if (plane == null && render)
			{
				GenerateMesh();
			}
			_material.SetShaderParameter("size", size);

			if (!simulate)
			{
				return;
			}
		}
		Time += (float)delta;
		_material.SetShaderParameter("time", Time);

	}
	#endregion

	private Wave[] GenerateSineWaves()
	{
		var waves = new Wave[42];
		var rng = new RandomNumberGenerator();
		float seed = 0.0f;
		float seedMod = rng.RandfRange(0f, 4f*Mathf.Pi);
		float frequency = baseFrequency;
		float amplitude = baseAmplitude;
		float phase = phase_modifier;
		for (int i = 0; i < 42; i++)
		{
			Wave wave = new Wave();
			float x = Mathf.Sin(seed);
			float y = Mathf.Cos(seed);
			wave.direction = new Vector2(x, y);
			wave.amplitude = amplitude;
			wave.frequency = frequency;
			wave.phase = phase;
			waves.SetValue(wave, i);
			
			seed += seedMod;//
			frequency *= gain;
			phase *= 1.07f;
			amplitude = Mathf.Lerp(amplitude, 0.0f, (1f - lacunarity)); // lacunarity;// * Mathf.Pow(lacunarity, i);
		}
		return waves;
	}

	private ImageTexture GenerateJONSWAP()
	{
		var image = Godot.Image.CreateEmpty((int)42, (int) 42, false, Image.Format.Rgbaf);
		var rng = new RandomNumberGenerator();
		
		ImageTexture tex = ImageTexture.CreateFromImage(image);
		return tex;
	}


	#region Helpers

	

	
	public void RegenerateMesh()
	{
		plane = null;
		GenerateMesh();
	}

	private void GenerateMesh()
	{
		if (plane != null) return;
		switch (_waterSystem)
		{
			case WaterSystem.SumOfSines:
				_material = _sumOfSinesMat;
				Wave[] waves = GenerateSineWaves();
				var input = new byte[32 * waves.Length];
				for (int i = 0; i < waves.Length; i++)
				{
					var in_float = new float[8];
					in_float[0] = waves[i].direction.X;
					in_float[1] = waves[i].direction.Y;
					in_float[2] = waves[i].amplitude;
					in_float[3] = waves[i].frequency;
					in_float[4] = waves[i].phase;
					in_float[5] = 0f;
					in_float[6] = 0f;
					in_float[7] = 0f;
					Buffer.BlockCopy(in_float, 0, input, 32*i, 32);
				}

				_material.SetShaderBuffer("waveBuffer", input);
				_material.SetShaderParameter("waveCount", 42);
				break;
			case WaterSystem.JONSWAP:
				_material = _JONSWAPWaterMat;
				break;
		}
		plane = new()
		{
			Size = size,
			SubdivideWidth = subdivide,
			SubdivideDepth = subdivide
		};
		Mesh = plane;
		Mesh.SurfaceSetMaterial(0, _material);

	}
	#endregion
}
