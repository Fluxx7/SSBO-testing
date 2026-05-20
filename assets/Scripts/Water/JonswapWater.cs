using System;
using System.Runtime.CompilerServices;
using Godot;
using GraphicsTesting.Libraries.ComputeShaderHandling;

namespace GraphicsTesting.assets.Scripts.Utility;

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
	// [ExportToolButton("Regenerate Waves")]
	// private Callable RegenWaves => Callable.From(RegenerateWaves);
	// [ExportToolButton("Simulate")]
	// private Callable Simulate => Callable.From(() => (_simulate = !_simulate));
	// [ExportToolButton("Update Waves")]
	// private Callable UpdateWaves => Callable.From(GenerateWaves);

	private ComputeShader jonswapGen = new("res://assets/Shaders/Compute/GLSL/Spectrums/jonswap_gen_fixed.glsl");
	private TessendorfFFTHandler fftGen;
	private float _currentSeed;
	private Vector3 _previousCameraLocation;
	private Basis _previousCameraBasis;
	private Vector3 _previousLocation;
	private float _time;
	private uint _texSize = 512;
	[Export] private float _tileSize = 1000f;

	private void InitShaders() {
		ByteBuffer jonswapParams = new ByteBuffer().Add([20f, 28f, 30f, 45f, 1000f, 3.3f, 5000f, _tileSize]);
		
		jonswapGen.CreateBuffer("jonswapParams", RenderingDevice.UniformType.UniformBuffer, 32u, 0, 0, jonswapParams.Generate());
		jonswapGen.CreateTexture("gaussian_noise", _texSize, _texSize, 0, 1);
		jonswapGen.CreateTexture("baseSpectrumTexture", _texSize, _texSize, 1, 0);
		ComputeShader.CreateTexture("spectrumTexture", _texSize, _texSize);
		ComputeShader.CreateTexture("displacementMap", _texSize, _texSize);
		ComputeShader.CreateTexture("gradientMap", _texSize, _texSize);
		
		ComputeShader.BindTextureParameter("displacementMap", 
			Callable.From(
				(Texture2Drd texUniform) => 
					_shader.SetShaderParameter("displacementMap", texUniform)));
		ComputeShader.BindTextureParameter("gradientMap", 
			Callable.From(
				(Texture2Drd texUniform) => 
					_shader.SetShaderParameter("gradientMap", texUniform)));
		
		fftGen = new TessendorfFFTHandler(_texSize, "baseSpectrumTexture", "spectrumTexture", "displacementMap", "gradientMap");
	}
	
	public override void _Ready() {
		if (Engine.IsEditorHint()) {
			_simulate = false;
		}
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
		jonswapGen.Close();
		base._ExitTree();
	}

	private void RegenerateWaves() {
		GenerateJonswap();
		GenerateWaves();
	}

	private void GenerateWaves() {
		fftGen.Run(_time, _tileSize, 500.0f);
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
		
		jonswapGen.Dispatch(_texSize / 16, _texSize / 16, 1, new ByteBuffer(_texSize));
		_shader.SetShaderParameter("mapWorldScale", new Vector4(_tileSize, 1f, .001f, .005f));
		
	}
}