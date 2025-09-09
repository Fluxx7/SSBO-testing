using System;
using Godot;

namespace GraphicsTesting.assets.Scripts.Utility;

/**
 * Projects a grid onto a plane for rendering water.
 * Generally based off of Claes Johanson's thesis paper:
 * [ Real-time water rendering: Introducing the projected grid concept ]
 * https://fileadmin.cs.lth.se/graphics/theses/projects/projgrid/projgrid-hq.pdf
 */
[Tool]
public partial class AlignedGridWater : MeshInstance3D {
	private ShaderMaterial _shader;
	[Export] public Vector2I Subdivide = new(256, 256);
	private Vector2I _prevSubdivide;
	[Export] public Camera3D Camera;
	private float _prevCameraFov;
	[Export] public uint WaveCount = 42u;
	[Export] public bool Simulate = true;

	[ExportGroup("Shader Parameters")] 
	
	private Vector4 _lowerPlane;

	[Export]
	public Vector4 LowerPlane {
		get => _lowerPlane;
		set {
			_lowerPlane = value;
			_shader?.SetShaderParameter("lowerPlane", value);
		}
	}

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

	private ComputeHandler _compHandler = new();
	private float _currentSeed;
	private Vector3 _previousCameraLocation;
	private Vector3 _previousLocation;
	private float _time;

	public override void _Ready() {
		_shader = new ShaderMaterial();
		_shader.SetShader(GD.Load<Shader>("res://assets/Shaders/projected_water.gdshader"));
		GenerateWaveBuffer();
		_shader.SetShaderParameter("water_scatter_color", ScatterColor);
		_shader.SetShaderParameter("air_bubble_color", BubbleColor);
		_shader.SetShaderParameter("water_color", WaterColor);
		_shader.SetShaderParameter("height_scale", HeightScale);
		_shader.SetShaderParameter("k2", K2);
		_shader.SetShaderParameter("k3", K3);
		_shader.SetShaderParameter("k4", K4);
		_shader.SetShaderParameter("air_bubble_density", BubbleDensity);
	}

	/**
	 * Align as closely as possible to the camera while obeying restrictions on position and rotation
	 * Create a grid covering the equivalent of the camera's near plane
	 */
	public override void _Process(double delta) {
		if (!IsOnScreen()) {
			return;
		}

		/*
		 * Need to determine the necessary extra size to add for the potential
		 */

		Camera ??= GetViewport().GetCamera3D();

		// align to camera
		float near = Camera.Near + 0.5f;
		if (Camera.GlobalTransform.Origin != _previousCameraLocation) {
			_previousCameraLocation = Camera.GlobalTransform.Origin;
			RenderingServer.GlobalShaderParameterSet("camera_coords", Camera.GlobalTransform.Origin);
		}
		Vector3 newPosition = Camera.GlobalTransform.Origin - Camera.GlobalTransform.Basis.Z * near;
		if (newPosition != _previousLocation) {
			_previousLocation = newPosition;
			Position = newPosition;
			Vector3 camRotation = Camera.RotationDegrees;

			/*
			 * Restrict camRotation to only angles that can't see the horizon
			 */
			


			if (camRotation.X >= 180f) {
				camRotation -= new Vector3(90f, 0f, 0f);
			} else if (camRotation.X <= -180f) {
				camRotation += new Vector3(270f, 0f, 0f);
			} else {
				camRotation -= new Vector3(90f, 0f, 0f);
			}

			RotationDegrees = camRotation;

			RenderingServer.GlobalShaderParameterSet("view_forward", -Camera.GlobalTransform.Basis.Z);
		}


		if (Mesh == null || _prevSubdivide != Subdivide || _prevCameraFov != Camera.Fov) {
			float maxWaveHeight = 10f * (1f - float.Pow(0.82f, WaveCount)) / 0.18f;
			float meshMargin = near + 0f;
			float fov = float.DegreesToRadians(Camera.Fov);
			_prevCameraFov = Camera.Fov;
			float height = 2f * meshMargin * float.Tan(fov / 2);
			float width = height * 16f / 9f;
			// float padding_factor = (meshMargin + maxWaveHeight) / meshMargin;
			// width *= padding_factor;
			// height *= padding_factor;

			Mesh = new PlaneMesh() {
				Size = new Vector2(width, height),
				SubdivideDepth = Subdivide.X,
				SubdivideWidth = Subdivide.Y,
				FlipFaces = true
			};
			_prevSubdivide = Subdivide;
			Mesh.SurfaceSetMaterial(0, _shader);
		}


		if (Simulate) {
			_time += (float)delta;
			_shader.SetShaderParameter("time", _time);
		}
	}

	private byte[] GeneratePushConstants() {
		var rng = new RandomNumberGenerator();
		byte[] pushConstants = new byte[16];
		float[] seedMod = [rng.RandfRange(0f, 4f * Mathf.Pi)];
		_currentSeed = seedMod[0];
		uint[] inputWaveCount = [WaveCount];
		Buffer.BlockCopy(inputWaveCount, 0, pushConstants, 0, sizeof(uint));
		Buffer.BlockCopy(seedMod, 0, pushConstants, sizeof(uint), sizeof(float));
		return pushConstants;
	}

	private void RegenerateWaves() {
		GenerateWaveBuffer();
	}

	private void GenerateWaveBuffer() {
		if (_compHandler.HasShader("buffer_gen") == false) {
			_compHandler.AddShader("buffer_gen",
				GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwavegen.glsl"));
			_compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
			var inputUniforms = new byte[32];

			float[] inputs = [10f, 0.02f, 1.5f, 0.82f, 1.18f];
			Buffer.BlockCopy(inputs, 0, inputUniforms, 0, sizeof(float) * 5);

			_compHandler.SetBuffer("paramBuffer", 20u, inputUniforms);
			_compHandler.AssignUniform("buffer_gen", "paramBuffer", 0, 0);
		}

		if (_compHandler.HasUniform("waveBuffer") == false) {
			RDUniform buffer = new RDUniform() {
				UniformType = RenderingDevice.UniformType.StorageBuffer
			};
			buffer.AddId(_compHandler.CreateBuffer("waveBuffer", RenderingDevice.UniformType.StorageBuffer,
				24 * WaveCount, "buffer_gen", 0, 1));
		}

		_compHandler.Dispatch("buffer_gen", WaveCount / 2, 1, 1, GeneratePushConstants());
		_shader?.SetShaderBufferRaw("waveBuffer", _compHandler.GetBufferData("waveBuffer"));
	}

	private bool IsOnScreen() {
		return true;
	}
}