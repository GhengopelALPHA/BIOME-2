namespace Biome2.Simulation;

/// <summary>
/// Controls simulation tick cadence, separate from render cadence.
/// Later, this will help support fast forward, fixed timestep, pause, and headless mode.
/// </summary>
public sealed class SimulationClock {
	private bool Paused { get; set; } = true;

	// Fixed timestep for deterministic updates later.
	private float FixedStepSeconds { get; set; } = 1.0f / 30.0f;

	// DelayTime: extra delay in seconds added to each simulation step. 0.0 = no extra delay.
	internal float DelayTime { get; set; } = 0.0f;

	private float _accumulatorSeconds;

	internal void SetPaused(bool paused) {
		Paused = paused;
	}

	public bool IsPaused() => Paused;

	public int ConsumeSteps(float dtSeconds) {
		if (Paused)
			return 0;

		_accumulatorSeconds += dtSeconds;
		int steps = 0;

		// Effective step interval includes the configured extra DelayTime (seconds).
		float effectiveStep = FixedStepSeconds + DelayTime;

		while (_accumulatorSeconds >= effectiveStep) {
			_accumulatorSeconds -= effectiveStep;
			steps++;
		}

		return steps;
	}
}
