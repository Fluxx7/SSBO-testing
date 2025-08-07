using System;
using Godot;

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
	[Export] public ShaderMaterial _sumOfSinesMat = null;
	[Export] public ShaderMaterial _JONSWAPWaterMat = null; // not implemented yet


	private PlaneMesh plane = null;
	private ImageTexture texture;
	[Export] public Vector2 size;
	[Export] public int subdivide;
	[Export] public uint waveCount = 4;

	[Export] public bool render = true;
	[Export] public bool simulate = false;

	[ExportGroup("Wave Parameters")]
	[Export] public float baseAmplitude = 1.0f;
	[Export] public float baseFrequency = 0.1f;
	[Export] public float gain = 1.18f;
	[Export] public float lacunarity = 0.82f;
	[Export] public float phase_modifier = 1.0f;
	private Vector2 moments;


	[ExportGroup("")]
	[ExportToolButton("Regenerate Mesh")]
	public Callable regenerate => Callable.From(RegenerateMesh);

	//AComputeC texture_gen;

	public void RegenerateMesh()
	{
		plane = null;
		GenerateMesh();
	}


	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		GenerateMesh();
	}

	private ImageTexture GenerateSineWaves()
	{
		var image = Godot.Image.CreateEmpty((int)waveCount, 2, false, Image.Format.Rgbf);
		var rng = new RandomNumberGenerator();

		moments = new();
		float seed = 0.0f;
		float frequency = baseFrequency;
		float amplitude = baseAmplitude;
		float phase = phase_modifier;
		for (int i = 0; i < waveCount; i++)
		{
			float x = Mathf.Sin(seed) + 1f;
			float y = Mathf.Cos(seed) + 1f;

			image.SetPixel(i, 0, new Color(x, y, 0));
			image.SetPixel(i, 1, new Color(amplitude, frequency, phase));
			

			// calculate moments
			moments.X += 0.5f * (amplitude * frequency * (x-1.0f)) * (amplitude * frequency * (x-1f));
			moments.Y += 0.5f * (amplitude * frequency * (y-1f)) * (amplitude * frequency * (y-1f));
			
			seed += rng.RandfRange(0f, Mathf.Pi);
			frequency *= gain;
			phase *= 1.07f;
			amplitude *= lacunarity;// * Mathf.Pow(lacunarity, i);
			//GD.Print("wave " + (i+1) + ": vec2(" + x + ", " + y + "), amplitude: " + amplitude + ", frequency:" + frequency + ", phase: " + phase);
		}
		ImageTexture tex = ImageTexture.CreateFromImage(image);
		return tex;
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



	private void GenerateMesh()
	{
		if (plane != null) return;
		texture = GenerateSineWaves();
		switch (_waterSystem)
		{
			// case WaterSystem.Gerstner:
			// 	_material = _gerstnerWaterMat;
			// 	break;
			case WaterSystem.SumOfSines:
				_material = _sumOfSinesMat;
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
		_material.SetShaderParameter("wave_texture", texture);
		_material.SetShaderParameter("waveCount", waveCount);
		_material.SetShaderParameter("moments", moments);
		Mesh.SurfaceSetMaterial(0, _material);

	}

	private void OnPropertyChange()
	{
		if (plane != null && !render)
		{
			plane = null;
			Mesh = null;
		}
		else if (render)
		{
			GenerateMesh();
		}
	}
}
