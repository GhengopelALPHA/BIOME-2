using Biome2.Diagnostics;

namespace Biome2.Simulation.Models;

/// <summary>
/// Simulation-side rule with resolved indices and ready-to-run reactants.
/// </summary>
public sealed class SimulationRuleModel(
    int layerIndex,
    int originSpeciesIndex,
    List<SimulationReactantModel> reactants,
    int newSpeciesIndex,
    double probability,
    string verboseRule,
    int? xMin = null,
    int? xMax = null,
    int? yMin = null,
    int? yMax = null,
    int moveSpeciesIndex = -1
) {
    public int LayerIndex { get; } = layerIndex;
    public int OriginSpeciesIndex { get; } = originSpeciesIndex;
    public List<SimulationReactantModel> Reactants { get; } = reactants ?? [];
    public int NewSpeciesIndex { get; } = newSpeciesIndex;
    public double Probability { get; } = probability;
    // Movement metadata
    public int MoveSpeciesIndex { get; } = moveSpeciesIndex;

    // Optional inclusive coordinate limits for this rule. Null means unbounded.
    public int? XMin { get; } = xMin;
    public int? XMax { get; } = xMax;
    public int? YMin { get; } = yMin;
    public int? YMax { get; } = yMax;

    private uint _opCount;
    public string VerboseRule { get; set; } = verboseRule;

    public void IncrementOpCount() => ++_opCount;

    public void ReportRuleDetails(bool shouldShowInConsole) {
        Logger.Info($"{VerboseRule}\t\t - {_opCount} operations", shouldShowInConsole);
    }
}
