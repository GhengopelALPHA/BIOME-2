using System.Collections.Generic;
using Biome2.Diagnostics;
using Biome2.FileLoading;

namespace Biome2.Simulation.Models;

/// <summary>
/// Simulation-side rule with resolved indices and ready-to-run reactants.
/// </summary>
public sealed class SimulationRule {
    public int LayerIndex { get; }
    public int OriginSpeciesIndex { get; }
    public List<SimulationReactant> Reactants { get; }
    public int NewSpeciesIndex { get; }
    public double Probability { get; }

    private uint _opCount;
    public string VerboseRule { get; set; }

    public SimulationRule(
		int layerIndex,
		int originSpeciesIndex,
		List<SimulationReactant> reactants,
		int newSpeciesIndex,
		double probability,
        string verboseRule
	) {
        LayerIndex = layerIndex;
        OriginSpeciesIndex = originSpeciesIndex;
        Reactants = reactants ?? new List<SimulationReactant>();
        NewSpeciesIndex = newSpeciesIndex;
        Probability = probability;
        VerboseRule = verboseRule;
    }

    public void IncrementOpCount() => ++_opCount;

	public void ReportRuleDetails() {
		Logger.Info($"{VerboseRule}\t\t - operation count: {_opCount}");
	}
}
