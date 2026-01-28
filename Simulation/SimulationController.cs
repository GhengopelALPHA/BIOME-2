using Biome2.World;
using Biome2.Diagnostics;

namespace Biome2.Simulation;

/// <summary>
/// Owns simulation state and stepping.
/// For now it does nothing. Later it will load rules, run multithread jobs, and collect stats.
/// </summary>
public sealed class SimulationController {
	private readonly WorldModel _world;

	public SimulationClock Clock { get; } = new();

	public SimulationController(WorldModel world) {
		_world = world;

		// Start paused, since we have no rules yet.
		Clock.Paused = true;
	}

	public void Update(float dtSeconds) {
		var steps = Clock.ConsumeSteps(dtSeconds);
		for (int i = 0; i < steps; i++) {
			StepOnce();
		}
	}

	private void StepOnce() {
		// Placeholder.
		// Future design:
		// 1) For each layer, run rule kernels over tiles in parallel into the write buffer.
		// 2) Swap buffers at end of tick for each layer.
		// 3) Record history and accumulate statistics.
		foreach (var layer in _world.Layers) {
			layer.Grid.CopyCurrentToNext();
			layer.Grid.SwapBuffers();
		}
	}
}
