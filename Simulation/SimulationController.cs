using Biome2.World;
using Biome2.Diagnostics;
using Biome2.FileLoading;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Biome2.Simulation;

/// <summary>
/// Owns simulation state and stepping.
/// For now it does nothing. Later it will load rules, run multithread jobs, and collect stats.
/// </summary>
public sealed class SimulationController : IDisposable {
    private WorldModel _world;
    private readonly object _stepLock = new object();

    // Background stepping
    private CancellationTokenSource? _cts;
    private Task? _bgTask;
    public bool IsBackgroundRunning => _bgTask != null && !_bgTask.IsCompleted && _cts != null && !_cts.IsCancellationRequested;

    // Event raised when the world instance is replaced by ApplyRules.
    public event Action<WorldModel>? WorldReplaced;

    // Loaded rules for the simulation. Populated by ApplyRules.
    private List<RulesModel> _rules = new();
    public IReadOnlyList<RulesModel> Rules => _rules;

    // Edge handling mode (influences neighbor lookups)
    private FileLoading.EdgeMode _edgeMode = FileLoading.EdgeMode.BORDER;

    // Indexed rules: key = (layerIndex, originSpeciesIndex)
    private Dictionary<(int layer, int origin), List<RulesModel>> _ruleIndex = new();
    // Layers that have any rules (for quick skipping)
    private HashSet<int> _layersWithRules = new();

    public SimulationClock Clock { get; } = new();

	public SimulationController(WorldModel world) {
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
    public void ApplyRules(RulesFileRequest request) {
        if (request is null) return;
        // Update paused state
        Clock.Paused = request.Paused;

        // Create a fresh world instance sized from the request (or fallback to current values)
        int newWidth = request.Width > 0 ? request.Width : _world.WidthCells;
        int newHeight = request.Height > 0 ? request.Height : _world.HeightCells;
        int newLayerCount = (request.Layers != null && request.Layers.Count > 0) ? request.Layers.Count : _world.LayerCount;

        var newWorld = new WorldModel(newWidth, newHeight, newLayerCount);

        // Apply species list to new world if provided
        if (request.Species != null && request.Species.Count > 0) {
            newWorld.SetSpeciesList(request.Species);
            // Initialize all layers to the first defined species (index 0)
            foreach (var layer in newWorld.Layers) {
                layer.Grid.Clear(0);
            }
        }

        // Update world layer names from the request if provided
        if (request.Layers != null && request.Layers.Count > 0) {
            int min = Math.Min(request.Layers.Count, newWorld.Layers.Count);
            for (int i = 0; i < min; i++) {
                newWorld.Layers[i].Name = request.Layers[i] ?? string.Empty;
            }
            if (request.Layers.Count > newWorld.Layers.Count) {
                Logger.Warn("More layer names provided by rules file than world contains; extra layer names ignored.");
            } else if (request.Layers.Count < newWorld.Layers.Count) {
                Logger.Warn("Fewer layer names provided by rules file than world contains; remaining layers keep default names.");
            }
        }

        // Prepare new rules list (copy) and edge mode locally
        var newRules = request.Rules != null ? request.Rules.ToList() : new List<RulesModel>();
        var newEdgeMode = request.Edges;

        // Validate and resolve names to indices against the new world, collect warnings and log them once.
        var warnings = RulesValidator.Validate(newRules, newWorld);
        foreach (var w in warnings) Logger.Warn(w);

        // Build rule index locally
        var newRuleIndex = new Dictionary<(int layer, int origin), List<RulesModel>>();
        var newLayersWithRules = new HashSet<int>();
        foreach (var r in newRules) {
            if (r.ResolvedLayerIndex < 0 || r.ResolvedOriginSpeciesIndex < 0) continue;
            var key = (r.ResolvedLayerIndex, r.ResolvedOriginSpeciesIndex);
            if (!newRuleIndex.TryGetValue(key, out var list)) {
                list = new List<RulesModel>();
                newRuleIndex[key] = list;
            }
            list.Add(r);
            newLayersWithRules.Add(r.ResolvedLayerIndex);
        }

        // Swap in new world and indexes under lock to avoid concurrent StepOnce access
        lock (_stepLock) {
            _world = newWorld;
            _rules = newRules;
            _edgeMode = newEdgeMode;
            _ruleIndex = newRuleIndex;
            _layersWithRules = newLayersWithRules;
        }

        // Notify subscribers outside lock
        WorldReplaced?.Invoke(_world);
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

		// Thread-local RNG to avoid contention and repeated seeds
		var threadLocalRand = new ThreadLocal<Random>(() => new Random(Random.Shared.Next()));

		Parallel.ForEach(layersToProcess, layerIndex => {
			var layer = _world.Layers[layerIndex];
			var grid = layer.Grid;
			var rand = threadLocalRand.Value!;

			for (int y = 0; y < grid.Height; y++) {
				for (int x = 0; x < grid.Width; x++) {
					byte originValue = grid.GetCurrent(x, y);

					// Lookup rules that match this layer and origin species
					if (!_ruleIndex.TryGetValue((layerIndex, originValue), out var candidates)) continue;

					foreach (var rule in candidates) {
						// Check all reactants
						bool allReactantsMatch = true;
						foreach (var react in rule.Reactants) {
							int targetLayerIndex = react.ResolvedLayerIndex >= 0 ? react.ResolvedLayerIndex : layerIndex;
							if (targetLayerIndex < 0 || targetLayerIndex >= _world.Layers.Count) { allReactantsMatch = false; break; }

							var targetGrid = _world.Layers[targetLayerIndex].Grid;

                            // Build neighbor species index span for the 8-neighborhood using edge handling based on _edgeMode.
                            Span<byte> neighborIdx = stackalloc byte[8];
                            int ni = 0;
                            for (int oy = -1; oy <= 1; oy++) {
                                for (int ox = -1; ox <= 1; ox++) {
                                    if (ox == 0 && oy == 0) continue;
                                    int nx = x + ox;
                                    int ny = y + oy;

                                    bool outOfX = nx < 0 || nx >= targetGrid.Width;
                                    bool outOfY = ny < 0 || ny >= targetGrid.Height;

                                    // Default to BORDER: neighbor ignored if out of bounds.
                                    bool useNeighbor = true;
                                    byte backupNeighborValue = 255; // sentinel non-matching species

									switch (_edgeMode) {
                                        case EdgeMode.WRAP:
                                            if (nx < 0) nx += targetGrid.Width;
                                            else if (nx >= targetGrid.Width) nx -= targetGrid.Width;
                                            if (ny < 0) ny += targetGrid.Height;
                                            else if (ny >= targetGrid.Height) ny -= targetGrid.Height;
                                            break;
                                        case EdgeMode.WRAPX:
                                            if (nx < 0) nx += targetGrid.Width;
                                            else if (nx >= targetGrid.Width) nx -= targetGrid.Width;
                                            if (outOfY) useNeighbor = false;
                                            break;
                                        case EdgeMode.WRAPY:
                                            if (ny < 0) ny += targetGrid.Height;
                                            else if (ny >= targetGrid.Height) ny -= targetGrid.Height;
                                            if (outOfX) useNeighbor = false;
                                            break;
                                        case EdgeMode.INFINITE:
                                            // INFINITE deferred: for now treat as BORDER (ignore out-of-bounds neighbors)
                                            if (outOfX || outOfY) useNeighbor = false;
											backupNeighborValue = 0; // "infinite" default space
											break;
                                        default:
                                            // BORDER
                                            if (outOfX || outOfY) useNeighbor = false;
                                            break;
                                    }

                                    neighborIdx[ni++] = useNeighbor ? targetGrid.GetCurrent(nx, ny) : backupNeighborValue;
                                }
                            }

							if (!react.Check(neighborIdx)) { allReactantsMatch = false; break; }
						}

						if (allReactantsMatch) {
							if (rule.ResolvedNewSpeciesIndex >= 0) {
								if (rule.Probability >= 1.0 || rand.NextDouble() < rule.Probability) {
									layer.Grid.SetNext(x, y, (byte)rule.ResolvedNewSpeciesIndex);
                                    rule.IncrementOpCount();
								}
							}
							// continue checking rules as some might have the same conditions but different probabilities or target species
						}
					}
				}
			}
		});

		threadLocalRand.Dispose();

		// Swap buffers after all rules evaluated
		foreach (var layer in _world.Layers) {
			layer.Grid.SwapBuffers();
		}
	}
}
