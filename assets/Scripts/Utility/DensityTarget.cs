using Godot;
using System;

[Tool]
public partial class DensityTarget : Camera3D
{
    public override void _Ready()
    {
        RenderingServer.GlobalShaderParameterSet("camera_coords", this.Position);
        base._Ready();
    }
    public override void _Process(double delta)
    {
        RenderingServer.GlobalShaderParameterSet("camera_coords", this.Position);
        base._Process(delta);
    }
}
