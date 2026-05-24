using Godot;
using System;

public partial class ControlWindow : Window {
	private uint flags;
	public override void _Ready() {
		RenderingServer.GlobalShaderParameterSet("renderFlags", 0b1111);
		flags = 0b1111;
		base._Ready();
	}

	public void SetFlag(bool state, int flag) {
		if (state) {
			flags |= (uint) flag;
		} else {
			flags &= ~((uint)flag);
		}
		RenderingServer.GlobalShaderParameterSet("renderFlags", flags);
	}
}
