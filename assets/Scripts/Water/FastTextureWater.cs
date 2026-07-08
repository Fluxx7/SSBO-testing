using System;
using System.Runtime.CompilerServices;
using Godot;

namespace GodotWaterRendering.assets.Scripts.Utility;

public partial class FastTextureWater : MeshInstance3D {
	private ShaderMaterial _shader;
	[Export] public Vector2I Subdivide = new(256, 256);
	[Export] public Vector2 Size = new(500f,500f);
	private Vector2I _prevSubdivide;
	private Vector2 _prevSize;
	[Export] public Camera3D Camera;
	[Export] private Window debugWindow;
	[Export] private Button debugWindowToggle;
	[Export] private Window visualsWindow;
	[Export] private Button visualsWindowToggle;
	private float _prevCameraFov;
	private bool _simulate = true;
	private FastWaterController waterController;

	#region shaderparams
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
	#endregion
	private float _currentSeed;
	private float _time;

	public void UpdateProperty(Variant value, StringName property) {
		switch (property) {
			case "BubbleColor":
				BubbleColor = value.AsColor();
				break;
			case "WaterColor":
				WaterColor = value.AsColor();
				break;
			case "ScatterColor":
				ScatterColor = value.AsColor();
				break;
		}
	}


	private void Simulate(bool simulate) {
		_simulate = simulate;
	}

	private void OnWaterControllerTextureUpdate(StringName texture_name, Texture2DArrayRD tex_rd) {
		_shader?.SetShaderParameter(texture_name, tex_rd);
	}
	
	public override void _Ready() {
		if (Engine.IsEditorHint()) {
			_simulate = false;
		}
		_shader = new ShaderMaterial();
		_shader.SetShader(GD.Load<Shader>("res://assets/Shaders/jonswap_water.gdshader"));
		
		waterController = GetNode<FastWaterController>("WaterController");
		waterController.TextureRidUpdated += OnWaterControllerTextureUpdate;
		waterController.CascadeCountChanged += cascades => _shader?.SetShaderParameter("cascade_count", cascades);
		waterController.TileLengthsChanged += lengths => _shader?.SetShaderParameter("tileLengths", lengths);
		waterController.PostTextures();
		
		_shader.SetShaderParameter("water_scatter_color", ScatterColor);
		_shader.SetShaderParameter("air_bubble_color", BubbleColor);
		_shader.SetShaderParameter("water_color", WaterColor);
		_shader.SetShaderParameter("height_scale", HeightScale);
		_shader.SetShaderParameter("k2", K2);
		_shader.SetShaderParameter("k3", K3);
		_shader.SetShaderParameter("k4", K4);
		_shader.SetShaderParameter("air_bubble_density", BubbleDensity);
		_shader.SetShaderParameter("tileLengths", waterController.TileLengths);
		_shader.SetShaderParameter("cascade_count", waterController.numCascades);
		
		if (debugWindow != null && debugWindowToggle != null) {
			InitDebugWindow();
		}
		if (visualsWindow != null && visualsWindowToggle != null) {
			InitVisualsWindow();
		}
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
		}
	}

	private static HBoxContainer CreateColorContainer(String text, Color default_value, ColorPickerButton.ColorChangedEventHandler colorChange) {
		var newContainer = new HBoxContainer();
		var colorLabel = new Label();
		colorLabel.Text = text + ":";
		
		var colorSelector = new ColorPickerButton();
		colorSelector.Color = default_value;
		colorSelector.Text = text;
		colorSelector.EditAlpha = false;
		colorSelector.ColorChanged += colorChange;
		
		var resetButton = new Button();
		resetButton.Pressed += () => {
			colorSelector.Color = default_value;
			colorChange.Invoke(default_value);
		};
		resetButton.Text = "Reset";
		
		newContainer.AddChild(colorLabel);
		newContainer.AddChild(colorSelector);
		newContainer.AddChild(resetButton);

		return newContainer;
	}
	
	private void InitDebugWindow() {
		
	}

	private void InitVisualsWindow() {
		var canvasLayer = new CanvasLayer();
		var visualControlsBox = new VBoxContainer();
		var colorParametersBox = new VBoxContainer();
		var lightingControlsBox = new HBoxContainer();
		
		HBoxContainer waterColorControls = CreateColorContainer("Water Color", _waterColor, (new_color) => {
			WaterColor = new_color;
		});
		HBoxContainer bubbleColorControls = CreateColorContainer("Air Bubble Color", _bubbleColor, (new_color) => {
			BubbleColor = new_color;
		});
		HBoxContainer scatterColorControls = CreateColorContainer("Scatter Color", _scatterColor, (new_color) => {
			ScatterColor = new_color;
		});
		colorParametersBox.AddChild(waterColorControls);
		colorParametersBox.AddChild(bubbleColorControls);
		colorParametersBox.AddChild(scatterColorControls);

		{
			var lightingLabel = new Label();
			lightingLabel.Text = "Lighting Controls";
			Func<string, string, Button> makeButton = (label, uniform_name) => {
				var newButton = new Button();
				newButton.Text = label;
				newButton.ToggleMode = true;
				newButton.ButtonPressed = true;
				newButton.Toggled += (pressed) => {
					_shader.SetShaderParameter(uniform_name, pressed);
				};
				return newButton;
			};

			Button diffuseButton = makeButton("Diffuse", "renderDiffuse");
			Button specularButton = makeButton("Specular", "renderSpecular");
			Button reflectionsButton = makeButton("Reflections", "renderReflections");
			Button foamButton = makeButton("Foam", "renderFoam");
			
			lightingControlsBox.AddChild(lightingLabel);
			lightingControlsBox.AddChild(diffuseButton);
			lightingControlsBox.AddChild(specularButton);
			lightingControlsBox.AddChild(reflectionsButton);
			lightingControlsBox.AddChild(foamButton);
		}
		

		visualControlsBox.AddChild(colorParametersBox);
		visualControlsBox.AddChild(lightingControlsBox);
		
		canvasLayer.AddChild(visualControlsBox);
		
		visualsWindow.AddChild(canvasLayer);
		
		
		
		visualsWindowToggle.Toggled += on => visualsWindow.SetVisible(on);
		visualsWindow.CloseRequested += () => visualsWindowToggle.SetPressed(false);
		visualsWindow.SetVisible(false);
		visualsWindowToggle.SetPressed(false);
	}
}