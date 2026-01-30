using Biome2.World;
using Biome2.Diagnostics;
using Biome2.FileLoading;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Biome2.FileLoading.Models;

namespace Biome2.Simulation;

/// <summary>
/// Owns simulation state and stepping.
/// For now it does nothing. Later it will load rules, run multithread jobs, and collect stats.
/// </summary>
public sealed class SimulationController : IDisposable {
    private WorldState _world;
    private readonly object _stepLock = new object();

    // Background stepping
    private CancellationTokenSource? _cts;
    private Task? _bgTask;
    public bool IsBackgroundRunning => _bgTask != null && !_bgTask.IsCompleted && _cts != null && !_cts.IsCancellationRequested;

    // Event raised when the world instance is replaced by ApplyRules.
    public event Action<WorldState>? WorldReplaced;

    // Loaded rules for the simulation. Populated by ApplyRules.
    private List<Models.SimulationRule> _rules = new();
    public IReadOnlyList<Models.SimulationRule> Rules => _rules;

    // Edge handling mode (influences neighbor lookups)
    private EdgeMode _edgeMode = EdgeMode.BORDER;

    // Indexed rules: key = (layerIndex, originSpeciesIndex)
    private Dictionary<(int layer, int origin), List<Models.SimulationRule>> _ruleIndex = new();
    // Layers that have any rules (for quick skipping)
    private HashSet<int> _layersWithRules = new();

    public SimulationClock Clock { get; } = new();

    // Immediate placement for visual feedback; writes both current and next buffers. Caller holds lock.
    public void PlaceImmediate(int layerIndex, int x, int y, byte speciesValue) {
        lock (_stepLock) {
            _world.PlaceImmediate(layerIndex, x, y, speciesValue);
        }
    }

    // Set selected species indices snapshot used by placement requests.
    public void SetSelectedSpeciesIndices(int[] indices) {
        _world.SetSelectedSpeciesIndices(indices);
    }

    // Enqueue a lightweight placement request (species chosen by WorldState snapshot during application).
    public void EnqueuePlacementRequest(int layerIndex, int x, int y) {
        _world.EnqueuePlacementRequest(layerIndex, x, y);
    }

    // Expose current world for UI and other subsystems that need read/write access.
    public WorldState World => _world;

    public SimulationController(WorldState world) {
        _world = world;

		// Start paused, since we have no rules yet.
		Clock.Paused = true;

		// Start background stepping so simulation can run as fast as possible independent of render.
		_cts = new CancellationTokenSource();
		_bgTask = Task.Run(() => BackgroundLoop(_cts.Token));
		// TODO: create default species and rules for Conway's Game of Life
	}

	public void Dispose() {
		if (_cts != null) {
			try { _cts.Cancel(); } catch { }
			try { _bgTask?.Wait(1000); } catch { }
			_cts.Dispose();
			_cts = null;
		}
	}

	private async Task BackgroundLoop(CancellationToken token) {
		// Run as fast as possible when not paused. Respect DelayTime as an optional extra pause per-step.
		try {
			while (!token.IsCancellationRequested) {
				if (Clock.Paused) {
					await Task.Delay(1, token).ConfigureAwait(false);
					continue;
				}

				// Perform a single step. Ensure not concurrently executed with any manual Update calls.
				lock (_stepLock) {
					StepOnce();
				}

				// Honor optional per-step delay (in seconds) if set; otherwise continue immediately for max speed.
				if (Clock.DelayTime > 0.0f) {
					int ms = (int)Math.Round(Clock.DelayTime * 1000.0f);
					if (ms > 0) await Task.Delay(ms, token).ConfigureAwait(false);
				}
			}
		} catch (OperationCanceledException) {
			// expected on cancellation
		}
	}

	// Apply rules and species from a parsed file request.
    public void ApplyRules(WorldModel request) {
        if (request is null) return;

        // Immediately apply pause setting so background loop respects it quickly.
        Clock.Paused = request.Paused;

        // Determine sizing: prefer file-provided positive values, otherwise keep current world values.
        int newWidth = request.Width > 0 ? request.Width : Math.Max(1, _world.WidthCells);
        int newHeight = request.Height > 0 ? request.Height : Math.Max(1, _world.HeightCells);
        int newLayerCount = (request.Layers != null && request.Layers.Count > 0) ? request.Layers.Count : Math.Max(1, _world.LayerCount);

        // Construct new runtime world state.
        var newWorld = new WorldState(newWidth, newHeight, newLayerCount);

		// If species list provided, apply it and initialize grids to species index 0.
		if (request.Species != null && request.Species.Count > 0) {
            newWorld.SetSpeciesList(request.Species);
            foreach (var layer in newWorld.Layers) {
                layer.Grid.Clear(0);
            }
        }

        // If layer names provided, copy them into the world model.
        if (request.Layers != null && request.Layers.Count > 0) {
            int min = Math.Min(request.Layers.Count, newWorld.Layers.Count);
            for (int i = 0; i < min; i++) newWorld.Layers[i].Name = request.Layers[i] ?? string.Empty;
            if (request.Layers.Count > newWorld.Layers.Count) Logger.Warn("More layer names provided by rules file than world contains; extra layer names ignored.");
            else if (request.Layers.Count < newWorld.Layers.Count) Logger.Warn("Fewer layer names provided by rules file than world contains; remaining layers keep default names.");
        }

        // Prepare rules and edge mode
        var fileRules = request.Rules ?? Array.Empty<RulesModel>();
        var newEdgeMode = request.Edges;

        // Convert file models to simulation models using the new world for name resolution.
        var (simRules, simIndex, simLayersWithRules, warnings) = RuleSetBuilder.Build(fileRules, newWorld);
        foreach (var w in warnings) Logger.Warn(w);

        // Swap world and rules under lock to avoid racing with stepping.
        lock (_stepLock) {
            _world = newWorld;
            _rules = simRules;
            _edgeMode = newEdgeMode;
            _ruleIndex = simIndex;
            _layersWithRules = simLayersWithRules;
        }

        // Notify subscribers after swap. Keep this outside the lock to avoid deadlocks.
        try {
            WorldReplaced?.Invoke(_world);
        } catch (Exception ex) {
            Logger.Error($"Exception while notifying WorldReplaced subscribers: {ex.Message}");
        }
    }

	public void Update(float dtSeconds) {
		// If background stepping is active, skip manual stepping to avoid contention.
		if (IsBackgroundRunning) return;

		var steps = Clock.ConsumeSteps(dtSeconds);
		for (int i = 0; i < steps; i++) {
			lock (_stepLock) {
				StepOnce();
			}
		}
	}

	private void StepOnce() {
		// Only prepare and process layers that have rules.
		var layersToProcess = _layersWithRules.Count > 0 ? _layersWithRules.ToArray() : Array.Empty<int>();

        // Prepare next buffers only for layers that will be processed.
        foreach (int li in layersToProcess) {
            _world.Layers[li].Grid.CopyCurrentToNext();
        }

        // Apply any pending manual placements queued by the UI into the next buffers now that they are prepared.
        _world.ApplyPendingPlacements();

        // Thread-local RNG to avoid contention and repeated seeds
        var threadLocalRand = new ThreadLocal<Random>(() => new Random(Random.Shared.Next()));

        Parallel.ForEach(layersToProcess, layerIndex => {
            var layer = _world.Layers[layerIndex];
            var grid = layer.Grid;
            var rand = threadLocalRand.Value!;
			int total = grid.Width * grid.Height;

			Parallel.For(0, total, idx => {
				var rand = threadLocalRand.Value!;
				int y = idx / grid.Width;
				int x = idx % grid.Width;
				byte originValue = grid.GetCurrent(x, y);

				bool appliedThisLayer = false;
				if (_ruleIndex.TryGetValue((layerIndex, originValue), out var candidates)) {
					foreach (var rule in candidates) {
						if (appliedThisLayer)
							break;

						bool allReactantsMatch = CheckAllReactantsMatch(layer, y, x, rule);
						if (allReactantsMatch) {
							if (rule.NewSpeciesIndex >= 0) {
								if (rule.Probability >= 1.0 || rand.NextDouble() < rule.Probability) {
									if (rule.LayerIndex == layerIndex) {
										layer.Grid.SetNext(x, y, (byte) rule.NewSpeciesIndex);
									} else {
										var targetLayer = _world.Layers[rule.LayerIndex];
										targetLayer.Grid.SetNext(x, y, (byte) rule.NewSpeciesIndex);
									}

									rule.IncrementOpCount();
									appliedThisLayer = true;
									break;
								}
							}
						}
					}
				}
			});
		});

        threadLocalRand.Dispose();

		// Swap buffers after all rules evaluated
		foreach (var layer in _world.Layers) {
			layer.Grid.SwapBuffers();
		}
	}

	private bool CheckAllReactantsMatch(WorldLayer layer, int y, int x, Models.SimulationRule rule) {
		bool allReactantsMatch = true;

        // Allocate neighbor index buffer once to avoid repeated stackalloc inside the loop (CA2014)
        Span<byte> neighborIdx = stackalloc byte[8];

        foreach (var react in rule.Reactants) {
			// If react.LayerIndex >= 0 then this reactant targets an exact layer cell at the same coordinates.
			if (react.LayerIndex >= 0) {
				int targetLayerIndex = react.LayerIndex;
				if (targetLayerIndex < 0 || targetLayerIndex >= _world.Layers.Count) { allReactantsMatch = false; break; }

				var targetGrid = _world.Layers[targetLayerIndex].Grid;

				// Bounds check for same-coordinate access
				if (x < 0 || x >= targetGrid.Width || y < 0 || y >= targetGrid.Height) { allReactantsMatch = false; break; }

				byte v = targetGrid.GetCurrent(x, y);
				if (v != react.SpeciesIndex) { allReactantsMatch = false; break; }
				// For exact-layer reactants, Count/Sign semantics are treated as == by design when referencing a single cell.
				continue;
            } else {
                // examine the 8-neighborhood on the current layer
                var targetGrid2 = layer.Grid;
                int ni = 0;
				for (int oy = -1; oy <= 1; oy++) {
					for (int ox = -1; ox <= 1; ox++) {
						if (ox == 0 && oy == 0)
							continue;
						int nx = x + ox;
						int ny = y + oy;

						bool outOfX = nx < 0 || nx >= targetGrid2.Width;
						bool outOfY = ny < 0 || ny >= targetGrid2.Height;

						// Default to BORDER: neighbor ignored if out of bounds.
						bool useNeighbor = true;
						byte backupNeighborValue = 255; // sentinel non-matching species

						switch (_edgeMode) {
							case EdgeMode.WRAP:
								if (nx < 0)
									nx += targetGrid2.Width;
								else if (nx >= targetGrid2.Width)
									nx -= targetGrid2.Width;
								if (ny < 0)
									ny += targetGrid2.Height;
								else if (ny >= targetGrid2.Height)
									ny -= targetGrid2.Height;
								break;
							case EdgeMode.WRAPX:
								if (nx < 0)
									nx += targetGrid2.Width;
								else if (nx >= targetGrid2.Width)
									nx -= targetGrid2.Width;
								if (outOfY)
									useNeighbor = false;
								break;
							case EdgeMode.WRAPY:
								if (ny < 0)
									ny += targetGrid2.Height;
								else if (ny >= targetGrid2.Height)
									ny -= targetGrid2.Height;
								if (outOfX)
									useNeighbor = false;
								break;
							case EdgeMode.INFINITE:
								// INFINITE deferred: for now treat as BORDER (ignore out-of-bounds neighbors)
								if (outOfX || outOfY)
									useNeighbor = false;
								backupNeighborValue = 0; // "infinite" default space
								break;
							default:
								// BORDER
								if (outOfX || outOfY)
									useNeighbor = false;
								break;
						}

						neighborIdx[ni++] = useNeighbor ? targetGrid2.GetCurrent(nx, ny) : backupNeighborValue;
					}
				}

				if (!react.Check(neighborIdx)) { allReactantsMatch = false; break; }
			}
		}

		return allReactantsMatch;
	}
}
