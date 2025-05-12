namespace Chickensoft.DiagramGenerator.Sample;

using System;
using Godot;

public abstract class InputUtilities
{
	/// <summary>
	///     Get axis pressed input by specifying two actions,
	///     one negative and one positive.
	/// </summary>
	/// <param name="negativeAction">Negative action name.</param>
	/// <param name="positiveAction">Positive action name.</param>
	/// <param name="axis">Joypad axis.</param>
	/// <param name="device">Device identifier.</param>
	/// <returns>
	///     A new <see cref="InputEventJoypadMotion" /> or
	///     <see cref="null" /> if AxisValue is in the DeadZone
	/// </returns>
	public static InputEventJoypadMotion? GetJoyPadActionPressedMotion(
		StringName negativeAction,
		StringName positiveAction,
		JoyAxis axis,
		int device = 0)
	{
		var axisValue = Input.GetAxis(negativeAction, positiveAction);
		var isInDeadZone =
			Math.Abs(axisValue) < InputMap.ActionGetDeadzone(negativeAction) ||
			Math.Abs(axisValue) < InputMap.ActionGetDeadzone(positiveAction);

		return
			!isInDeadZone && axisValue != 0 &&
			(
				Input.IsActionPressed(negativeAction) ||
				Input.IsActionPressed(positiveAction)
			)
				? new InputEventJoypadMotion
				{
					Axis = axis,
					AxisValue = axisValue,
					Device = device
				}
				: null;
	}

	public static (bool Left, bool right) IsRotating()
	{
		var rotateLeftPressed = Input.IsActionPressed(GameInputs.RotateLeft);
		var rotateLeftJustPressed = Input.IsActionJustPressed(GameInputs.RotateLeft);

		var rotateRightPressed = Input.IsActionPressed(GameInputs.RotateRight);
		var rotateRightJustPressed = Input.IsActionJustPressed(GameInputs.RotateRight);

		return (rotateLeftPressed || rotateLeftJustPressed,
			rotateRightPressed || rotateRightJustPressed);
	}

	public static (bool In, bool Out) IsZooming()
	{
		var zoomInPressed = Input.IsActionPressed(GameInputs.ZoomIn);
		var zoomInJustPressed = Input.IsActionJustPressed(GameInputs.ZoomIn);

		var zoomOutPressed = Input.IsActionPressed(GameInputs.ZoomOut);
		var zoomOutJustPressed = Input.IsActionJustPressed(GameInputs.ZoomOut);

		return (zoomInPressed || zoomInJustPressed,
			zoomOutPressed || zoomOutJustPressed);
	}
}