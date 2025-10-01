using System;
using System.Runtime.CompilerServices;
using Godot;

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
	[ExportToolButton("Clear Shaders")]
	private Callable ClearShaders => Callable.From(() => {
		_compHandler.Close();
		_compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
	});

	private ComputeHandler _compHandler = new();
	private float _currentSeed;
	private Vector3 _previousCameraLocation;
	private Basis _previousCameraBasis;
	private Vector3 _previousLocation;
	private float _time;
	private uint _texSize = 64;

	public override void _Ready() {
		_compHandler = new ComputeHandler(RenderingServer.GetRenderingDevice());
		_shader = new ShaderMaterial();
		_shader.SetShader(GD.Load<Shader>("res://assets/Shaders/jonswap_water.gdshader"));
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
		if (!IsOnScreen()) {
			return;
		}
	
		
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
		}
	}
	
	public override void _ExitTree() {
		_compHandler?.Close();
		base._ExitTree();
	}

	private void RegenerateWaves() {
		GenerateJonswap();
		GenerateWaves();
	}

	private void GenerateWaves() {
		if (!_compHandler.HasShader("jonswap_gen") || !_compHandler.HasUniform("spectrumTexture")) {
			GenerateJonswap();
		}
		if (!_compHandler.HasShader("make_maps")) {
			_compHandler.AddShader("make_maps", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/JONSWAP/tessendorf_idft_2d.glsl"));
			_compHandler.AssignUniform("make_maps", "spectrumTexture", 0, 0);
			Texture2Drd displacementTexture = new Texture2Drd();
			Texture2Drd gradientTexture = new Texture2Drd();
			displacementTexture.TextureRdRid = _compHandler.CreateTexture("displacementMap", _texSize, _texSize);
			_compHandler.AssignUniform("make_maps", "displacementMap", 1, 0);
			gradientTexture.TextureRdRid = _compHandler.CreateTexture("gradientMap", _texSize, _texSize);
			_compHandler.AssignUniform("make_maps", "gradientMap", 1, 1);
			Texture2Drd normalTexture = new Texture2Drd();
			normalTexture.TextureRdRid = _compHandler.CreateTexture("normalMap", _texSize, _texSize);
			_compHandler.AssignUniform("make_maps", "normalMap", 1, 2);
			_shader.SetShaderParameter("displacementMap", displacementTexture);
			_shader.SetShaderParameter("gradientMap", gradientTexture);
			_shader.SetShaderParameter("normalMap", normalTexture);
			
		}
		
		byte[] push_constants = new byte[16];
		Buffer.BlockCopy(new []{_texSize}, 0, push_constants, 0, sizeof(int));
		Buffer.BlockCopy(new []{_time, 64f, 10000f}, 0, push_constants, sizeof(int), sizeof(float) * 3);

		_compHandler.Dispatch("make_maps", _texSize / 16,  _texSize / 16, 1, push_constants);
	}

	private bool IsOnScreen() {
		return true;
	}

	private void GenerateJonswap() {
		if (!_compHandler.HasShader("jonswap_gen")) {
			_compHandler.AddShader("jonswap_gen", GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/JONSWAP/jonswap_gen.glsl"));
			byte[] jonswapParams = new byte[32];
			Buffer.BlockCopy(new []{18f, 20f, 1f, 0f, 10000f, 3.3f, 10000f, 64f}, 0, jonswapParams, 0, 32);
			_compHandler.CreateBuffer("jonswapParams", RenderingDevice.UniformType.UniformBuffer, 32u, "jonswap_gen", 0, 0, jonswapParams);
			
			Texture2Drd gaussianTexture = new Texture2Drd();
			Texture2Drd spectrumTexture = new Texture2Drd();
			gaussianTexture.TextureRdRid = _compHandler.CreateTexture("gaussian_noise", _texSize, _texSize);
			_compHandler.AssignUniform("jonswap_gen", "gaussian_noise", 0, 1);
			spectrumTexture.TextureRdRid = _compHandler.CreateTexture("spectrumTexture", _texSize, _texSize);
			_compHandler.AssignUniform("jonswap_gen", "spectrumTexture", 1, 0);
			_shader.SetShaderParameter("gaussian", gaussianTexture);
			_shader.SetShaderParameter("spectrumTexture", spectrumTexture);
		}

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

		_compHandler.SetTexture("gaussian_noise", _texSize, _texSize, gaussian);
		
		byte[] push_constants = new byte[16];
		Buffer.BlockCopy(new []{_texSize}, 0, push_constants, 0, sizeof(int));
		
		_compHandler.Dispatch("jonswap_gen", _texSize / 16, _texSize / 16, 1, push_constants);
		
	}
}