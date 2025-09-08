using System;
using Godot;
using Godot.Collections;

namespace GraphicsTesting.assets.Scripts.Utility;

/**
 * Projects a grid onto a plane for rendering water.
 * Generally based off of Claes Johanson's thesis paper:
 * [ Real-time water rendering: Introducing the projected grid concept ]
 * https://fileadmin.cs.lth.se/graphics/theses/projects/projgrid/projgrid-hq.pdf
 */
[Tool]
public partial class WaterProjector : MeshInstance3D {
	private Vector4 upperPlane;
	private Vector4 lowerPlane;
	private Vector3 viewRotation;
	private Vector3 viewPosition;

	public ShaderMaterial shader;
	[Export] public Vector2I subdivide = new(256,256);
	private Vector2I prev_subdivide;
	[Export] public Camera3D camera;
	[Export] public uint waveCount = 42u;
	[Export] public bool simulate = true;

	[ExportGroup("Shader Parameters")] 
	private Color _scatterColor;
	[Export] public Color scatterColor {
		get => _scatterColor;
		set {
			_scatterColor = value; if (shader != null) shader.SetShaderParameter("water_scatter_color", value); }
	}
	
	private Color _bubbleColor;
	[Export] public Color bubbleColor {
		get => _bubbleColor;
		set {
			_bubbleColor = value; if (shader != null) shader.SetShaderParameter("air_bubble_color", value); }
	}
	private Color _waterColor;
	[Export] public Color waterColor {
		get => _waterColor;
		set {
			_waterColor = value; if (shader != null) shader.SetShaderParameter("water_color", value); }
	}

	private float _heightScale;
	[Export] public float heightScale {
		get => _heightScale;
		set {
			_heightScale = value; if (shader != null) shader.SetShaderParameter("height_scale", value);
		}
	}
	
	private float _k2;
	[Export] public float k2 {
		get => _k2;
		set {
			_k2 = value; if (shader != null) shader.SetShaderParameter("k2", value);
		}
	}
	
	private float _k3;
	[Export] public float k3 {
		get => _k3;
		set {
			_k3 = value; if (shader != null) shader.SetShaderParameter("k3", value);
		}
	}
	
	private float _k4;
	[Export] public float k4 {
		get => _k4;
		set {
			_k4 = value; if (shader != null) shader.SetShaderParameter("k4", value);
		}
	}
	
	private float _bubbleDensity;
	[Export] public float bubbleDensity {
		get => _bubbleDensity;
		set {
			_bubbleDensity = value; if (shader != null) shader.SetShaderParameter("air_bubble_density", value);
		}
	}
	[ExportGroup("")]
	[ExportToolButton("Regenerate Waves")]
	public Callable regenWaves => Callable.From(RegenerateWaves);

	private ComputeHandler compHandler = new();
	private float currentSeed;
	private Vector3 previousCameraLocation;
	private float Time;

	public override void _Ready() {
		shader = new ShaderMaterial();
		shader.SetShader(GD.Load<Shader>("res://assets/Shaders/projected_water.gdshader"));
		GenerateWaveBuffer();
		shader.SetShaderParameter("water_scatter_color", scatterColor);
		shader.SetShaderParameter("air_bubble_color", bubbleColor);
		shader.SetShaderParameter("water_color", waterColor);
		shader.SetShaderParameter("height_scale", heightScale);
		shader.SetShaderParameter("k2", k2);
		shader.SetShaderParameter("k3", k3);
		shader.SetShaderParameter("k4", k4);
		shader.SetShaderParameter("air_bubble_density", bubbleDensity);
	}

	/**
	 * Align as closely as possible to the camera while obeying restrictions on position and rotation
	 * Create a grid covering the equivalent of the camera's near plane
	 */
	public override void _Process(double delta) {
		if (!IsOnScreen()) {
			return;
		}
		
		
		// align to camera
		float near = camera.Near + 0.5f;
		Vector3 newPosition = camera.GlobalTransform.Origin -camera.GlobalTransform.Basis.Z * near;
		if (newPosition != previousCameraLocation) {
			previousCameraLocation = newPosition;
			LookAtFromPosition(newPosition,camera.GlobalTransform.Origin, camera.GlobalBasis.Y);
			Rotation += new Vector3(float.Pi/2f, 0f, 0f); // there's probably a better solution than this
			RenderingServer.GlobalShaderParameterSet("view_forward", camera.GlobalTransform.Basis.Z);
		}
		
		if (Mesh == null || prev_subdivide != subdivide) {
			float maxWaveHeight = 10f * (1f - float.Pow(0.82f, waveCount)) / 0.18f;
			float meshMargin = near + 0.5f;
			float fov =  float.DegreesToRadians(camera.Fov);
			float height = 2f * meshMargin * float.Tan(fov / 2);
			float width = height * 16f / 9f;
			height *= 1.1f;
			// float padding_factor = (meshMargin + maxWaveHeight) / meshMargin;
			// width *= padding_factor;
			// height *= padding_factor;

			Mesh = new PlaneMesh() {
				Size = new Vector2(width, height),
				SubdivideDepth = subdivide.X,
				SubdivideWidth = subdivide.Y,
				FlipFaces = true
			};
			prev_subdivide = subdivide;
			Mesh.SurfaceSetMaterial(0, shader);
		}
		

		if (simulate) {
			Time += (float)delta;
			shader.SetShaderParameter("time", Time);
		}
	}

	private byte[] GeneratePushConstants() {
		var rng = new RandomNumberGenerator();
		byte[] pushConstants = new byte[16];
		float[] seedMod = [rng.RandfRange(0f, 4f * Mathf.Pi)];
		currentSeed = seedMod[0];
		uint[] inputWaveCount = [waveCount];
		Buffer.BlockCopy( inputWaveCount, 0, pushConstants, 0, sizeof(uint));
		Buffer.BlockCopy( seedMod, 0, pushConstants, sizeof(uint), sizeof(float));
		return pushConstants;
	}
	
	public void RegenerateWaves() {
		GenerateWaveBuffer();
	}

	private void GenerateWaveBuffer() {
		if (compHandler.HasShader("buffer_gen") == false) {
			compHandler.AddShader("buffer_gen",GD.Load<RDShaderFile>("res://assets/Shaders/Compute/GLSL/sinwavegen.glsl"));
			compHandler.CreateBuffer("paramBuffer", RenderingDevice.UniformType.UniformBuffer, 32);
			var inputUniforms = new byte[32];
			
			float[] inputs = [10f, 0.02f, 1.5f, 0.82f, 1.18f];
			Buffer.BlockCopy( inputs, 0, inputUniforms, 0, sizeof(float) * 5);
		
			compHandler.SetBuffer("paramBuffer", 20u, inputUniforms);
			compHandler.AssignUniform("buffer_gen", "paramBuffer", 0, 0);
		}

		if (compHandler.HasUniform("waveBuffer") == false) {
			RDUniform buffer = new RDUniform() {
				UniformType = RenderingDevice.UniformType.StorageBuffer
			};
			buffer.AddId(compHandler.CreateBuffer("waveBuffer", RenderingDevice.UniformType.StorageBuffer, 24 * waveCount, "buffer_gen", 0, 1));
		}
		compHandler.Dispatch("buffer_gen", waveCount / 2, 1, 1, GeneratePushConstants());
		shader.SetShaderBufferRaw("waveBuffer", compHandler.GetBufferData("waveBuffer"));
	}

	private bool IsOnScreen() {
		return true;
	}
}