using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Biome2.Input;

/// <summary>
/// Centralized, frame coherent input snapshot.
/// Later, ImGui can mark when it wants to capture mouse and keyboard input.
/// </summary>
public sealed class InputState {
	public float MouseX { get; private set; }
	public float MouseY { get; private set; }
	public float MouseDeltaX { get; private set; }
	public float MouseDeltaY { get; private set; }
	public float MouseWheelDelta { get; private set; }

	public bool MouseLeftDown { get; private set; }
	public bool MouseRightDown { get; private set; }
	public bool MouseMiddleDown { get; private set; }

	public bool KeyW { get; private set; }
	public bool KeyA { get; private set; }
	public bool KeyS { get; private set; }
	public bool KeyD { get; private set; }

	public bool KeyShift { get; private set; }
	public bool KeyCtrl { get; private set; }

	// Signals from ImGui whether it wants to capture input. App should honor these to block world interaction.
	public bool GuiWantsMouse { get; private set; }
	public bool GuiWantsKeyboard { get; private set; }

	private float _lastMouseX;
	private float _lastMouseY;

	public void UpdateFrom(GameWindow window) {
		var mouse = window.MouseState;
		var keyboard = window.KeyboardState;

		MouseX = mouse.X;
		MouseY = mouse.Y;

		MouseDeltaX = MouseX - _lastMouseX;
		MouseDeltaY = MouseY - _lastMouseY;

		_lastMouseX = MouseX;
		_lastMouseY = MouseY;

		// OpenTK exposes scroll delta since last poll.
		MouseWheelDelta = mouse.ScrollDelta.Y;

		MouseLeftDown = mouse.IsButtonDown(MouseButton.Left);
		MouseRightDown = mouse.IsButtonDown(MouseButton.Right);
		MouseMiddleDown = mouse.IsButtonDown(MouseButton.Middle);

        KeyW = keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.W);
        KeyA = keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.A);
        KeyS = keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.S);
        KeyD = keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D);

        KeyShift = keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftShift) || keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightShift);
        KeyCtrl = keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl) || keyboard.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightControl);
	}

	// Called by ImGuiController to set capture flags.
	public void SetGuiWants(bool mouse, bool keyboard) {
		GuiWantsMouse = mouse;
		GuiWantsKeyboard = keyboard;
	}
}
