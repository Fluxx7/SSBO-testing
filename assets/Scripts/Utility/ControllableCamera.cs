using System;
using Godot;

namespace GraphicsTesting.assets.Scripts.Utility;

public partial class ControllableCamera : Camera3D {
	[Export] public float Sensitivity = 3f;
	[Export] public float DefaultVelocity = 5f;
	[Export] public float SpeedScale = 1.17f;
	[Export] public float BoostSpeedMultiplier = 3f;
	[Export] public float MaxSpeed = 1000f;
	[Export] public float MinSpeed = 0.2f;
	private float _velocity;

	public override void _Ready() {
		_velocity = DefaultVelocity;
	}

	public override void _Input(InputEvent @event) {
		if (!Current) {
			return;
		}

		if (Input.GetMouseMode() == Input.MouseModeEnum.Captured) {
			Vector3 rotation = Rotation;
			if (@event is InputEventMouseMotion motion) {
				rotation.Y -= motion.Relative.X/1000f * Sensitivity;
				rotation.X -= motion.Relative.Y/1000f * Sensitivity;
				rotation.X = float.Clamp(rotation.X, float.Pi / -2f, float.Pi / 2f);
			}

			Rotation = rotation;
		}
		
		if (@event is InputEventMouseButton button) {
			switch (button.ButtonIndex) {
				case MouseButton.Right:
					Input.SetMouseMode(button.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible);
					break;
				case MouseButton.WheelUp:
					_velocity = float.Clamp(_velocity * SpeedScale, MinSpeed, MaxSpeed);
					break;
				case MouseButton.WheelDown:
					_velocity = float.Clamp(_velocity / SpeedScale, MinSpeed, MaxSpeed);
					break;
				default:
					break;
			}
		}
	}

	public override void _Process(double delta) {
		if (!Current) {
			return;
		}

		Vector3 movement = new();
		if (Input.IsPhysicalKeyPressed(Key.D)) {
			movement.X += 1f;
		}
		if (Input.IsPhysicalKeyPressed(Key.A)) {
			movement.X -= 1f;
		}
		if (Input.IsPhysicalKeyPressed(Key.E)) {
			movement.Y += 1f;
		}
		if (Input.IsPhysicalKeyPressed(Key.Q)) {
			movement.Y -= 1f;
		}
		if (Input.IsPhysicalKeyPressed(Key.S)) {
			movement.Z += 1f;
		}
		if (Input.IsPhysicalKeyPressed(Key.W)) {
			movement.Z -= 1f;
		}

		Vector3 normalMovement = movement.Normalized();

		Vector3 baseMovement = normalMovement * _velocity * (float) delta;

		if (Input.IsPhysicalKeyPressed(Key.Shift)) {
			TranslateObjectLocal(baseMovement * BoostSpeedMultiplier);
		} else {
			TranslateObjectLocal(baseMovement);
		}

	}
}