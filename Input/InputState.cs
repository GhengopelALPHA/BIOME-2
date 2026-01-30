using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;

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

    // Right-drag state for panning
    private bool _rightMouseWasDown;
    private bool _rightDragStarted;
    private Vector2 _rightDragStart;

    // Placement state for left-button painting
    private bool _placing;
    private int _lastPlacedX = -1;
    private int _lastPlacedY = -1;

    // Snapshot of selected species indices for painting
    private int[] _selectedSpecies = Array.Empty<int>();

    // Set snapshot of selected species indices (called from UI)
    public void SetSelectedSpeciesIndices(int[] indices) {
        _selectedSpecies = indices ?? Array.Empty<int>();
    }

    // Central entry for handling interactions that involve camera panning and painting.
    public void HandleInteractions(Graphics.Camera camera, Graphics.Renderer renderer, Simulation.SimulationController simulation) {
        // If UI wants the mouse, do nothing.
        if (GuiWantsMouse) {
            // reset drag/placing states to avoid stale state
            _rightMouseWasDown = false;
            _rightDragStarted = false;
            EndPlacement();
            return;
        }

        // Right-button panning
        if (MouseRightDown) {
            if (!_rightMouseWasDown) {
                _rightMouseWasDown = true;
                _rightDragStarted = false;
                _rightDragStart = new Vector2(MouseX, MouseY);
            } else {
                if (!_rightDragStarted) {
                    var dx = MouseX - _rightDragStart.X;
                    var dy = MouseY - _rightDragStart.Y;
                    if (dx * dx + dy * dy > 4.0f * 4.0f) {
                        _rightDragStarted = true;
                        var drag = new Vector2(MouseDeltaX, -MouseDeltaY);
                        camera.PanBy(drag);
                    }
                } else {
                    var drag = new Vector2(MouseDeltaX, -MouseDeltaY);
                    camera.PanBy(drag);
                }
            }
        } else {
            _rightMouseWasDown = false;
            _rightDragStarted = false;
        }

        // Left-button painting
        if (MouseLeftDown) {
            HandlePlacement(camera, renderer, simulation);
        } else {
            EndPlacement();
        }
    }

    // End active placement (called when left button released)
    public void EndPlacement() {
        _placing = false;
        _lastPlacedX = -1;
        _lastPlacedY = -1;
    }

    // Handle placement action: picks cell under cursor and enqueues request if cell changed.
    public void HandlePlacement(Graphics.Camera camera, Graphics.Renderer renderer, Simulation.SimulationController simulation) {
        // Do not act when UI wants mouse
        if (GuiWantsMouse) return;

        var worldPos = camera.ScreenToWorld(new OpenTK.Mathematics.Vector2(MouseX, MouseY), camera.Zoom);
        float cs = renderer.CellSize;
        int cellX = (int)Math.Floor(worldPos.X / cs);
        int cellY = (int)Math.Floor(worldPos.Y / cs);

        if (!_placing || cellX != _lastPlacedX || cellY != _lastPlacedY) {
            // snapshot selected species indices
            var sel = _selectedSpecies;
            if (sel.Length == 0) return;

            simulation.SetSelectedSpeciesIndices(sel);
            simulation.EnqueuePlacementRequest(simulation.World.ActiveLayerIndex, cellX, cellY);

            // Request immediate visual update (renderer uses world's current buffer)
            //renderer.UploadSingleCell(simulation.World.ActiveLayer.Grid, cellX, cellY);

            _lastPlacedX = cellX;
            _lastPlacedY = cellY;
            _placing = true;
        }
    }

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
