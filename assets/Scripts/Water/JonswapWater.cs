using System;
using System.Runtime.CompilerServices;
using Godot;
using GraphicsTesting.Libraries.ComputeShaderHandling;

namespace GraphicsTesting.assets.Scripts.Utility;

/**
 * Projects a grid onto a plane for rendering water.
 * Generally based off of Claes Johanson's thesis paper:
 * [ Real-time water rendering: Introducing the projected grid concept ]
 * https://fileadmin.cs.lth.se/graphics/theses/projects/projgrid/projgrid-hq.pdf
 */
[Tool]
public partial class JonswapWater : MeshInstance3D {
	private ShaderMaterial _shader;
	[Export] public Vector2I Subdivide = new(256, 256);
	[Export] public Vector2 Size = new(500f,500f);
	private Vector2I _prevSubdivide;
	private Vector2 _prevSize;
	[Export] public Camera3D Camera;
	private float _prevCameraFov;
	private bool _simulate = true;

	[ExportGroup("Shader Parameters")] 

	private Color _scatterColor = new(0x00b82aFF);

	[Export]
	public Color ScatterColor {
		get => _scatterColor;
		set {
			_scatterColor = value;
			_shader?.SetShaderParameter("water_scatter_color", value);
		}
	}

	private Color _bubbleColor = new(0x0d1b2aFF);

	[Export]
	public Color BubbleColor {
		get => _bubbleColor;
		set {
			_bubbleColor = value;
			_shader?.SetShaderParameter("air_bubble_color", value);
		}
	}

	private Color _waterColor = new(0x001548FF);

	[Export]
	public Color WaterColor {
		get => _waterColor;
		set {
			_waterColor = value;
			_shader?.SetShaderParameter("water_color", value);
		}
	}

	private float _heightScale = 21.1f;

	[Export]
	public float HeightScale {
		get => _heightScale;
		set {
			_heightScale = value;
			_shader?.SetShaderParameter("height_scale", value);
		}
	}

	private float _k2 = 13.52f;

	[Export]
	public float K2 {
		get => _k2;
		set {
			_k2 = value;
			_shader?.SetShaderParameter("k2", value);
		}
	}

	private float _k3 = 5.42f;

	[Export]
	public float K3 {
		get => _k3;
		set {
			_k3 = value;
			_shader?.SetShaderParameter("k3", value);
		}
	}

	private float _k4 = 4.75f;

	[Export]
	public float K4 {
		get => _k4;
		set {
			_k4 = value;
			_shader?.SetShaderParameter("k4", value);
		}
	}

	private float _bubbleDensity = 3.4f;

	[Export]
	public float BubbleDensity {
		get => _bubbleDensity;
		set {
			_bubbleDensity = value;
			_shader?.SetShaderParameter("air_bubble_density", value);
		}
	}

	[ExportGroup("")]
	[ExportToolButton("Regenerate Waves")]
	private Callable RegenWaves => Callable.From(RegenerateWaves);
	[ExportToolButton("Simulate")]
	private Callable Simulate => Callable.From(() => (_simulate = !_simulate));
	[ExportToolButton("Update Waves")]
	private Callable UpdateWaves => Callable.From(GenerateWaves);

	private ComputeShader jonswapGen = new("res://assets/Shaders/Compute/GLSL/JONSWAP/jonswap_gen.glsl");
	private ComputeShader mapGen = new("res://assets/Shaders/Compute/GLSL/JONSWAP/tessendorf_idft_2d.glsl");
	private float _currentSeed;
	private Vector3 _previousCameraLocation;
	private Basis _previousCameraBasis;
	private Vector3 _previousLocation;
	private float _time;
	private uint _texSize = 128;
	[Export] private float _tileMult = 4f;

	private void InitShaders() {
		byte[] jonswapParams = new byte[32];
		Buffer.BlockCopy(new []{18f, 20f, 1f, 0f, 10000f, 3.3f, 10000f, _texSize * _tileMult}, 0, jonswapParams, 0, 32);
		jonswapGen.CreateBuffer("jonswapParams", RenderingDevice.UniformType.UniformBuffer, 32u, 0, 0, jonswapParams);
		jonswapGen.CreateTexture("gaussian_noise", _texSize, _texSize, 0, 1);
		jonswapGen.CreateTexture("spectrumTexture", _texSize, _texSize, 1, 0);

		mapGen.AssignUniform("spectrumTexture", 0, 0);
		mapGen.CreateTexture("displacementMap", _texSize, _texSize, 1, 0);
		mapGen.CreateTexture("gradientMap", _texSize, _texSize, 1, 1);
		mapGen.CreateTexture("normalMap", _texSize, _texSize, 1, 2);
		
		mapGen.BindTextureParameter("displacementMap", 
			Callable.From(
				(Texture2Drd texUniform) => 
					_shader.SetShaderParameter("displacementMap", texUniform)));
		mapGen.BindTextureParameter("gradientMap", 
			Callable.From(
				(Texture2Drd texUniform) => 
					_shader.SetShaderParameter("gradientMap", texUniform)));
	}
	
	public override void _Ready() {
		_shader = new ShaderMaterial();
		_shader.SetShader(GD.Load<Shader>("res://assets/Shaders/jonswap_water.gdshader"));
		InitShaders();
		GenerateJonswap();
		GenerateWaves();
		_shader.SetShaderParameter("water_scatter_color", ScatterColor);
		_shader.SetShaderParameter("air_bubble_color", BubbleColor);
		_shader.SetShaderParameter("water_color", WaterColor);
		_shader.SetShaderParameter("height_scale", HeightScale);
		_shader.SetShaderParameter("k2", K2);
		_shader.SetShaderParameter("k3", K3);
		_shader.SetShaderParameter("k4", K4);
		_shader.SetShaderParameter("air_bubble_density", BubbleDensity);
		_shader.SetShaderParameter("mapWorldScale", new Vector4(1f/(_texSize * _tileMult), -0.2f, 1f, 1f));
		Camera ??= GetViewport().GetCamera3D();
	}

	public override void _Process(double delta) {
	
		
		Camera ??= GetViewport().GetCamera3D();


		if (Mesh == null || _prevSubdivide != Subdivide || _prevSize != Size) {
			Mesh = new PlaneMesh() {
				Size = Size,
				SubdivideDepth = Subdivide.X,
				SubdivideWidth = Subdivide.Y
			};
			_prevSubdivide = Subdivide;
			_prevSize = Size;
			Mesh.SurfaceSetMaterial(0, _shader);
		}


		if (_simulate) {
			_time += (float)delta;
			GenerateWaves();
		}
	}
	
	public override void _ExitTree() {
		base._ExitTree();
	}

	private void RegenerateWaves() {
		GenerateJonswap();
		GenerateWaves();
	}

	private void GenerateWaves() {
		byte[] push_constants = new byte[16];
		Buffer.BlockCopy(new []{_texSize}, 0, push_constants, 0, sizeof(int));
		Buffer.BlockCopy(new []{_time, _texSize * _tileMult, 10000f}, 0, push_constants, sizeof(int), sizeof(float) * 3);

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
		
		jonswapGen.Dispatch(_texSize / 16, _texSize / 16, 1, push_constants);
		
	}
}