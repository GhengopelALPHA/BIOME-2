namespace Biome2.Diagnostics;

/// <summary>
/// Replace with Serilog or similar later.
/// Keeping it tiny for now.
/// </summary>
public static class Logger {
	// Simple lock to serialize console writes and avoid interleaved output
	private static readonly object _consoleLock = new();

	public static void Info(string message) {
		lock (_consoleLock) {
			Console.WriteLine($"[INFO] {message}");
			Console.Out.Flush();
		}
	}

	public static void Warn(string message) {
		lock (_consoleLock) {
			Console.WriteLine($"[WARN] {message}");
			Console.Out.Flush();
		}
	}

	public static void Error(string message) {
		lock (_consoleLock) {
			Console.WriteLine($"[ERROR] {message}");
			Console.Out.Flush();
		}
	}
}
