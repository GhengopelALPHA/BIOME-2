namespace Biome2.Diagnostics;

/// <summary>
/// Replace with Serilog or similar later.
/// Keeping it tiny for now.
/// </summary>
public static class Logger {
	public static void Info(string message) => Console.WriteLine($"[INFO] {message}");
	public static void Warn(string message) => Console.WriteLine($"[WARN] {message}");
	public static void Error(string message) => Console.WriteLine($"[ERROR] {message}");
}
