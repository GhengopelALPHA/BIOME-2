namespace Biome2.Diagnostics;

/// <summary>
/// Lightweight perf tracking hooks.
/// Later, feed these into an ImGui overlay.
/// </summary>
public sealed class Performance {
	public double LastUpdateSeconds { get; private set; }
	public double LastRenderSeconds { get; private set; }

	private long _updateStart;
	private long _renderStart;

	public void BeginUpdate(double dt) => _updateStart = StopwatchTicks();
	public void EndUpdate() => LastUpdateSeconds = TicksToSeconds(StopwatchTicks() - _updateStart);

	public void BeginRender(double dt) => _renderStart = StopwatchTicks();
	public void EndRender() => LastRenderSeconds = TicksToSeconds(StopwatchTicks() - _renderStart);

	private static long StopwatchTicks() => System.Diagnostics.Stopwatch.GetTimestamp();

	private static double TicksToSeconds(long ticks) {
		return ticks / (double) System.Diagnostics.Stopwatch.Frequency;
	}
}
