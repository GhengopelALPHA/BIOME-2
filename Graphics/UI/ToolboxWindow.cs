using ImGuiNET;
using System.Numerics;
using System;
using System.IO;
using System.Globalization;
using Biome2.Diagnostics;
using Biome2.Simulation;
using System.Windows.Forms;

namespace Biome2.Graphics.UI;

internal sealed class ToolboxWindow
{
    private const string Title = "Toolbox";
	private const string LoadRulesButtonLabel = "Load Rules";
	private const string DebugRulesButtonLabel = "Debug Rules";

	// Stored delay values to avoid recalculating every frame. They are updated
	// only when the user moves the slider.
	// Defaults: delay = 0, slider position = 0.
	private float CurrentDelay { get; set; } = 0.0f;
    private int DelayMs { get; set; } = 0;
    // Cached slider position.
    private float DelaySliderPos { get; set; } = 0.0f;

    private float _windowWidth;

    private static ImGuiWindowFlags GetWindowFlags()
    {
        return ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBringToFrontOnFocus;
    }

    public void Render(Renderer renderer, SimulationController simulation)
    {
        // Force the UI to a fixed screen location and prevent it from being moved by dragging.
        // Use 0,0 so the UI is positioned at the top-left of the application viewport.
        ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);

        // AlwaysAutoResize will size to content, but enforce size constraints
        ImGui.SetNextWindowSizeConstraints(new Vector2(260, 90), new Vector2(float.MaxValue, float.MaxValue), null);

        ImGui.Begin(Title, GetWindowFlags());

        // Load Rules button (centered)
        {
            var textSize = ImGui.CalcTextSize(LoadRulesButtonLabel);
            var framePadding = ImGui.GetStyle().FramePadding;
            var buttonWidth = textSize.X + framePadding.X * 2.0f;
            ImGui.SetCursorPosX((_windowWidth - buttonWidth) * 0.5f);
        }
        if (ImGui.Button(LoadRulesButtonLabel)) {
            // Attempt to show a native file dialog per-platform.
            string? selected = null;

            try {
                // Use WinForms OpenFileDialog directly since app now targets Windows.
                try {
                    using var ofd = new System.Windows.Forms.OpenFileDialog();
                    ofd.Filter = "Rules files|*.bio;*.txt|All files|*.*";
                    ofd.Multiselect = false;
                    // Start dialog in the directory where the executable resides.
                    try {
                        ofd.InitialDirectory = AppContext.BaseDirectory;
                    } catch {
                        // ignore if unable to set
                    }
                    var res = ofd.ShowDialog();
                    if (res == System.Windows.Forms.DialogResult.OK) selected = ofd.FileName;
                } catch (Exception ex) {
                    Logger.Error($"Windows file dialog failed: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(selected) && File.Exists(selected)) {
                    var loader = new FileLoading.RulesLoader();
                    var request = loader.Load(selected);

                    // Apply to simulation controller via new ApplyRules API
                    simulation.ApplyRules(request);
                }
            } catch (Exception ex) {
                Logger.Error($"Failed to load rules file: {ex.Message}");
            }
        }

        // Pause toggle
        bool paused = simulation.Clock.Paused;
        if (ImGui.Checkbox("Paused", ref paused)) {
            simulation.Clock.Paused = paused;
        }

		// ShowGrid toggle
		bool showGrid = renderer.ShowGrid;
		if (ImGui.Checkbox("Show Grid", ref showGrid)) {
			renderer.ShowGrid = showGrid;
		}

		// Show Axes toggle
		bool showAxes = renderer.ShowAxes;
		if (ImGui.Checkbox("Show Axes", ref showAxes)) {
			renderer.ShowAxes = showAxes;
		}

        // Delay time slider: use a nonlinear curve so mid slider values yield small millisecond delays.
        // Mapping: DelayTime (seconds) = slider^expo * 1.0 (max 1s). Inverse used to position the slider.
        const int expo = 5; // exponent chosen such that 0.5^5 ~= 0.03125 -> ~31ms

        ImGui.Text($"Tick Delay: {DelayMs} ms");
        // Use cached slider position; it defaults to 0 and is updated only when
        // the user moves the control.
        float sliderPos = DelaySliderPos;
        ImGui.PushItemWidth(-1);
        if (ImGui.SliderFloat("##TickDelayTime", ref sliderPos, 0.0f, 1.0f)) {
            // store new slider position
            DelaySliderPos = sliderPos;
            float newDelay = MathF.Pow(Math.Clamp(sliderPos, 0.0f, 1.0f), expo);
            simulation.Clock.DelayTime = newDelay;
            CurrentDelay = newDelay;
            DelayMs = (int)Math.Round(CurrentDelay * 1000.0f);
        }
        ImGui.PopItemWidth();

		ImGui.Separator();

        // Debug Rules button: reconstruct a readable rule line from the simulation's stored rules
        {
            var textSize = ImGui.CalcTextSize(DebugRulesButtonLabel);
            var framePadding = ImGui.GetStyle().FramePadding;
            var buttonWidth = textSize.X + framePadding.X * 2.0f;
            ImGui.SetCursorPosX((_windowWidth - buttonWidth) * 0.5f);
        }
        if (ImGui.Button(DebugRulesButtonLabel)) {
			var rules = simulation.Rules;
			if (rules == null || rules.Count == 0) {
				Logger.Info("Debug Rules: no rules loaded.");
			} else {
                Logger.Info("  ===== Rules =====");
				foreach (var r in rules) {
					r.ReportRuleDetails();
				}
                Logger.Info("  === End Rules ===");
			}
		}

		_windowWidth = ImGui.GetWindowWidth();

		ImGui.End();
	}
}
