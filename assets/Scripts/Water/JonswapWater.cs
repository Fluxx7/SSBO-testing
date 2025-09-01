using System;
using Godot;
using Godot.Collections;
using Godot.NativeInterop;

using Wave = Godot.Collections.Dictionary<Godot.StringName, float>;
[Tool]
public partial class JonswapWater : MeshInstance3D {
	[Export] public float Time = 0.0f;





	private ShaderMaterial _material;
	[ExportGroup("Materials")]
	[Export] public ShaderMaterial _JONSWAPWaterMat = null;
	[ExportGroup("")] private PlaneMesh plane = null;
	[Export] public Vector2 size;
	[Export] public int subdivide;
	
	[Export] public bool render = true;
	[Export] public bool simulate = false;

	private ImageTexture texture;
	[Export] public uint waveCount = 42;

	[ExportGroup("Spectrum Parameters")] 
	[Export] public float fetch;

	public enum TextureSize {
		t256 = 256,
		t512 = 512,
		t1024 = 1024,
	}


	private Array<Wave> waves;
	[ExportGroup("JONSWAP")] 
	[Export] public TextureSize texSize = TextureSize.t1024;
	[Export] public uint numTextures = 4;

	[ExportGroup("")]
	[ExportToolButton("Regenerate Mesh")]
	public Callable regenerate => Callable.From(RegenerateMesh);
	[ExportToolButton("Regenerate Waves")]
	public Callable regenWaves => Callable.From(RegenerateWaves);


	public void generateSpectrum() {
		
	}

	public void RegenerateWaves() {
		plane = null;
		GenerateMesh();
	}

	#region Overrides

	public override void _Ready() {
		if (Engine.IsEditorHint()) return;
		GenerateMesh();
	}

	public override void _Process(double delta) {
		if (Engine.IsEditorHint()) {
			if (waves == null) return;
			if (plane != null && !render) {
				plane = null;
				Mesh = null;
			} else if (plane == null && render) {
				GenerateMesh();
			}
			
			if (_material == null) return;

			_material.SetShaderParameter("size", size);

			if (!simulate) {
				return;
			}
		}

		Time += (float)delta;
		_material.SetShaderParameter("time", Time);
	}

	#endregion

	

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
	

	private void GenerateMesh() {
		if (plane != null) return;
		_material = _JONSWAPWaterMat;
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