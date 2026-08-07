using System;
using Godot;
using Godot.Collections;
using Range = Godot.Range;

namespace GodotWaterRendering.assets.Scripts.Utility;

public partial class FastTextureWater : MeshInstance3D {
	private ShaderMaterial _shader;
	private Shader _debugTexShader;
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
	private SpinBox layerSelector = new();
	private SpinBox mipSelector = new();
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
	private Dictionary<StringName, ShaderMaterial> _debugRectShaderMats = new();
	
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
		if (_debugRectShaderMats.TryGetValue(texture_name, out ShaderMaterial shaderMat)) {
			shaderMat.SetShaderParameter("debug_tex", tex_rd);
		} else if (texture_name == "gradFoamMaps") {
			if (_debugRectShaderMats.TryGetValue("gradientMaps", out ShaderMaterial gradShaderMat)) {
				gradShaderMat.SetShaderParameter("debug_tex", tex_rd);
			}
			if (_debugRectShaderMats.TryGetValue("foamMaps", out ShaderMaterial foamShaderMat)) {
				foamShaderMat.SetShaderParameter("debug_tex", tex_rd);
			}
		}

	}
	
	public override void _Ready() {
		if (Engine.IsEditorHint()) {
			_simulate = false;
		}

		_debugTexShader = GD.Load<Shader>("res://assets/Shaders/debug_texture.gdshader");
		_shader = new ShaderMaterial();
		_shader.SetShader(GD.Load<Shader>("res://assets/Shaders/jonswap_water.gdshader"));
		
		waterController = GetNode<FastWaterController>("WaterController");
		waterController.TextureRidUpdated += OnWaterControllerTextureUpdate;
		waterController.CascadeCountChanged += cascades => {
			layerSelector.MaxValue = cascades - 1;
			_shader?.SetShaderParameter("cascade_count", cascades);
		};
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
		layerSelector.MaxValue = waterController.numCascades - 1;
		
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

	private VBoxContainer CreateDebugTexColorRect(StringName label, StringName tex_name) {
		var mainContainer = new VBoxContainer();

		var newLabel = new Label();
		newLabel.Text = label;
		newLabel.HorizontalAlignment = HorizontalAlignment.Center;
		
		mainContainer.AddChild(newLabel);
		
		var newRect = new ColorRect();
		var shaderMat = new ShaderMaterial();
		shaderMat.SetShader(_debugTexShader);
		_debugRectShaderMats[tex_name] = shaderMat;
		newRect.Material = shaderMat;
		newRect.CustomMinimumSize = new Vector2(256f, 256f);

		mainContainer.AddChild(newRect);
		return mainContainer;
	}
	
	private void InitDebugWindow() {
		var canvasLayer = new CanvasLayer();
		var mainBox = new VBoxContainer();
		var texBox = new VBoxContainer();
		var buttonBox = new HBoxContainer();

		var upperBox = new HBoxContainer();
		var lowerBox = new HBoxContainer();
		
		upperBox.AddChild(CreateDebugTexColorRect("Initial Spectrum Map", "baseSpectrum"));
		upperBox.AddChild(CreateDebugTexColorRect("Spectrum Map", "spectrumTexture"));
		upperBox.AddChild(CreateDebugTexColorRect("Displacement Map", "heightMaps"));
		lowerBox.AddChild(CreateDebugTexColorRect("Gradient Map", "gradientMaps"));
		lowerBox.AddChild(CreateDebugTexColorRect("Foam Map", "foamMaps"));
		_debugRectShaderMats["gradientMaps"].SetShaderParameter("tex_type", 1);
		_debugRectShaderMats["foamMaps"].SetShaderParameter("tex_type", 2);
		lowerBox.Alignment = BoxContainer.AlignmentMode.Center;
		waterController?.PostTextures();
		
		texBox.AddChild(upperBox);
		texBox.AddChild(lowerBox);

		var newLabel = new Label();
		newLabel.Text = "Debug Render Mode:";

		var newBox = new HBoxContainer();
		newBox.AddChild(newLabel);

		var newButton = new OptionButton();
		newButton.AddItem("Off");
		newButton.AddItem("Displacement");
		newButton.AddItem("Gradients");
		newButton.Selected = 0;
		newButton.ItemSelected += index => _shader?.SetShaderParameter("debugRender", index);
		
		newBox.AddChild(newButton);
		var vBox = new HBoxContainer();
		vBox.Alignment = BoxContainer.AlignmentMode.Center;
		vBox.AddChild(newBox);

		newBox = new HBoxContainer();
		newLabel = new Label();
		newLabel.Text = "Cascade:";
		
		newBox.AddChild(newLabel);
		
		layerSelector.Step = 1.0;
		layerSelector.MinValue = 0.0;
		layerSelector.ValueChanged += value => {
			_shader.SetShaderParameter("debugCascade", (int) value);
			foreach (var (_ ,shader_mat) in _debugRectShaderMats) {
				shader_mat.SetShaderParameter("debug_tex_index", (int) value);
				
			}
		}; 
		
		newBox.AddChild(layerSelector);
		vBox.AddChild(newBox);
		
		newBox = new HBoxContainer();
		newLabel = new Label();
		newLabel.Text = "Mipmap Level:";
		
		newBox.AddChild(newLabel);
		
		
		mipSelector.Step = 1.0;
		mipSelector.MinValue = 0.0;
		mipSelector.ValueChanged += value => {
			foreach (var (_ ,shader_mat) in _debugRectShaderMats) {
				shader_mat.SetShaderParameter("mip_level", (int) value);
				
			}
		}; 
		
		newBox.AddChild(mipSelector);
		vBox.AddChild(newBox);
		buttonBox.AddChild(vBox);
		
		
		mainBox.AddChild(texBox);
		mainBox.AddChild(buttonBox);
		
		canvasLayer.AddChild(mainBox);
		debugWindow.AddChild(canvasLayer);
		debugWindowToggle.Toggled += on => debugWindow.SetVisible(on);
		debugWindow.CloseRequested += () => debugWindowToggle.SetPressed(false);
		debugWindow.SetVisible(false);
		debugWindowToggle.SetPressed(false);
	}

	private HBoxContainer createFloatSelector(string text, float starting_value, StringName uniform_name, float min_val = 0f, float max_val = 100f, float step = 1f, bool allow_greater = false) {
		var newContainer = new HBoxContainer();
		var colorLabel = new Label();
		colorLabel.Text = text;
		
		var valueSelector = new SpinBox();
		valueSelector.MinValue = min_val;
		valueSelector.MaxValue = max_val;
		valueSelector.AllowGreater = allow_greater;
		valueSelector.Step = step;
		valueSelector.Value = starting_value;
		valueSelector.ValueChanged += (new_val) => {
			_shader?.SetShaderParameter(uniform_name, new_val);
		};
		
		newContainer.AddChild(colorLabel);
		newContainer.AddChild(valueSelector);
		return newContainer;
	}
	
	private HBoxContainer createFloatSelectorAction(string text, float starting_value, Range.ValueChangedEventHandler action, float min_val = 0f, float max_val = 100f, float step = 1f, bool allow_greater = false) {
		var newContainer = new HBoxContainer();
		var colorLabel = new Label();
		colorLabel.Text = text;
		
		var valueSelector = new SpinBox();
		valueSelector.MinValue = min_val;
		valueSelector.MaxValue = max_val;
		valueSelector.AllowGreater = allow_greater;
		valueSelector.Step = step;
		valueSelector.Value = starting_value;
		valueSelector.ValueChanged += action;
		
		newContainer.AddChild(colorLabel);
		newContainer.AddChild(valueSelector);
		return newContainer;
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

		var subParametersBox = new HBoxContainer();
		
		var shaderParamatersBox = new VBoxContainer();
		{
			var shaderParamLabel = new Label();
			shaderParamLabel.Text = "Water Shader Parameters";
			shaderParamatersBox.AddChild(shaderParamLabel);
		}
		shaderParamatersBox.AddChild(createFloatSelector("Height Scale", _heightScale, "height_scale", 0f, 50f, 0.01f, true));
		shaderParamatersBox.AddChild(createFloatSelector("K2", _k2, "k2", 0f, 20f, 0.01f, true));
		shaderParamatersBox.AddChild(createFloatSelector("K3", _k3, "k3", 0f, 20f, 0.01f, true));
		shaderParamatersBox.AddChild(createFloatSelector("K4", _k4, "k4", 0f, 20f, 0.01f, true));
		shaderParamatersBox.AddChild(createFloatSelector("Bubble Density", _bubbleDensity, "air_bubble_density", 0f, 20f, 0.01f, true));

		var foamParamsTabs = new TabContainer(); 
		{
			for (var i = 0; i < FastWaterController.MAX_CASCADES; i++) {
				var foamParametersBox = new VBoxContainer();
				foamParametersBox.Name = $"Cascade {i}";
				{
					var foamParamLabel = new Label();
					foamParamLabel.Text = "Foam Parameters";
					foamParametersBox.AddChild(foamParamLabel);
				}
				var index = i;
				foamParametersBox.AddChild(createFloatSelectorAction("Whitecap", 0.5f,
					value => {waterController?.SetWhitecap(index, (float) value); }, 0f, 50f, 0.01f, true));
				foamParametersBox.AddChild(createFloatSelectorAction("Foam Amount", 0.5f,
					value => {waterController?.SetFoamAmount(index, (float) value); }, 0f, 50f, 0.01f, true));
				foamParamsTabs.AddChild(foamParametersBox);
			}
		}
		subParametersBox.AddChild(shaderParamatersBox);
		subParametersBox.AddChild(foamParamsTabs);
		
		colorParametersBox.AddChild(subParametersBox);
		
		
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
		var mipsButton = new Button();
		mipsButton.Text = "Use Mipmaps";
		mipsButton.ToggleMode = true;
		mipsButton.ButtonPressed = true;
		mipsButton.Toggled += (pressed) => { waterController?.UseMips(pressed); };

		visualControlsBox.AddChild(colorParametersBox);
		visualControlsBox.AddChild(lightingControlsBox);
		visualControlsBox.AddChild(mipsButton);
		
		canvasLayer.AddChild(visualControlsBox);
		
		visualsWindow.AddChild(canvasLayer);
		
		
		
		visualsWindowToggle.Toggled += on => visualsWindow.SetVisible(on);
		visualsWindow.CloseRequested += () => visualsWindowToggle.SetPressed(false);
		visualsWindow.SetVisible(false);
		visualsWindowToggle.SetPressed(false);
	}
}