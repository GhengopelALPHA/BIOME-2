namespace Biome2.Simulation;

/// <summary>
/// Controls simulation tick cadence, separate from render cadence.
/// Later, this will help support fast forward, fixed timestep, pause, and headless mode.
/// </summary>
public sealed class SimulationClock {
	public bool Paused { get; set; } = true;

	// Fixed timestep for deterministic updates later.
	public float FixedStepSeconds { get; set; } = 1.0f / 30.0f;

	private float _accumulatorSeconds;

	public int ConsumeSteps(float dtSeconds) {
		if (Paused)
			return 0;

		_accumulatorSeconds += dtSeconds;
		int steps = 0;

		while (_accumulatorSeconds >= FixedStepSeconds) {
			_accumulatorSeconds -= FixedStepSeconds;
			steps++;
		}

		return steps;
	}
}
