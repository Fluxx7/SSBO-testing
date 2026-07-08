using Godot;
using System;

public partial class DebugWindow : Window
{
	public void DebugRenderControl(int debug) {
		RenderingServer.GlobalShaderParameterSet("debugRender", debug);
	}
}
