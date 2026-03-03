using System;
using System.IO;
using System.Windows.Forms;
using Biome2.Diagnostics;

namespace Biome2.Graphics.UI;

internal static class FileLoaderUI
{
    // Prompt the user for a rules file and return the selected path, or null if none selected
    // or an error occurred. This function is UI-only and does not apply the file to the
    // simulation; callers in SimulationController should handle applying and error handling.
    public static string? PromptForRulesFile()
    {
        string? selected = null;

        try {
            try {
                using var ofd = new OpenFileDialog();
                ofd.Filter = "Rules files|*.bio;*.txt|All files|*.*";
                ofd.Multiselect = false;
                try {
                    ofd.InitialDirectory = AppContext.BaseDirectory;
                } catch {
                    // ignore if unable to set
                }

                var res = ofd.ShowDialog();
                if (res == DialogResult.OK) selected = ofd.FileName;
            } catch (Exception ex) {
                Logger.Error($"Windows file dialog failed: {ex.Message}");
            }
        } catch (Exception ex) {
            Logger.Error($"Unexpected error showing file dialog: {ex.Message}");
        }

        return selected;
    }
}
