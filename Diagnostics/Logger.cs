using System.IO;

namespace Biome2.Diagnostics;

public static class Logger {
    // Simple lock to serialize writes and avoid interleaved output
    private static readonly object _outputLock = new();

    // StreamWriter for file logging. Null if file cannot be opened.
    private static readonly StreamWriter? _logWriter;

    enum LogLevel {
        INFO,
        WARN,
        ERROR
	}

	// Static ctor: create/overwrite log file in executable directory and register exit handler to dispose
	static Logger() {
        try {
            var logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");
            var fs = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _logWriter = new StreamWriter(fs) { AutoFlush = true };
        }
        catch {
            // If we can't create the log file, continue without file logging.
            _logWriter = null;
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            lock (_outputLock) {
                try {
                    _logWriter?.Flush();
                    _logWriter?.Dispose();
                }
                catch { /* ignore */ }
            }
        };
    }

    private static void Log(LogLevel level, string message, bool logConsole) {
        var line = $"[{level}] {message}";
        lock (_outputLock) {
            if (logConsole) {
                Console.WriteLine(line);
                Console.Out.Flush();
            }
            try {
                if (_logWriter is not null) {
                    _logWriter.WriteLine(line);
                }
            }
            catch {
                Console.WriteLine($"[CRITICAL] Failed to write to log file: {line}");
			}
        }
    }

    public static void Info(string message, bool logConsole = true) => Log(LogLevel.INFO, message, logConsole);

    public static void Warn(string message, bool logConsole = true) => Log(LogLevel.WARN, message, logConsole);

    public static void Error(string message, bool logConsole = true) => Log(LogLevel.ERROR, message, logConsole);
}
