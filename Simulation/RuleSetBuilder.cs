using System.Collections.Generic;
using Biome2.World;
using Biome2.Simulation.Models;
using Biome2.FileLoading.Models;

namespace Biome2.Simulation;

/// <summary>
/// Converts file-loading models into simulation-ready structures.
/// Returns warnings encountered during conversion.
/// </summary>
public static class RuleSetBuilder {
    public static (List<SimulationRule> rules, Dictionary<(int layer, int origin), List<SimulationRule>> index, HashSet<int> layersWithRules, List<string> warnings)
        Build(IReadOnlyList<RulesModel> fileRules, WorldState world)
    {
        var warnings = new List<string>();
        var simRules = new List<SimulationRule>();
        var index = new Dictionary<(int layer, int origin), List<SimulationRule>>();
        var layersWithRules = new HashSet<int>();

        if (fileRules == null) return (simRules, index, layersWithRules, warnings);

        for (int i = 0; i < fileRules.Count; i++) {
            var fr = fileRules[i];

            int layerIdx = world.GetLayerIndex(fr.LayerName);
            if (layerIdx < 0) { warnings.Add($"Rule #{i+1}: unknown layer '{fr.LayerName}'"); continue; }

            int originIdx = world.GetSpeciesIndex(fr.OriginSpeciesName);
            if (originIdx < 0) { warnings.Add($"Rule #{i+1}: unknown origin species '{fr.OriginSpeciesName}'"); continue; }

            int newIdx = world.GetSpeciesIndex(fr.NewSpeciesName);
            if (newIdx < 0) { warnings.Add($"Rule #{i+1}: unknown new species '{fr.NewSpeciesName}'"); continue; }

            var simReactants = new List<SimulationReactant>();
            foreach (var r in fr.Reactants) {
                int sidx = world.GetSpeciesIndex(r.SpeciesName);
                if (sidx < 0) { warnings.Add($"Rule #{i+1}: reactant unknown species '{r.SpeciesName}'"); continue; }

                int lidx;
                if (string.IsNullOrEmpty(r.LayerName)) {
                    // No explicit layer specified in the reactant: use -1 to indicate "use neighborhood on the rule's layer".
                    lidx = -1;
                } else {
                    lidx = world.GetLayerIndex(r.LayerName);
                    if (lidx < 0) { warnings.Add($"Rule #{i+1}: reactant unknown layer '{r.LayerName}'"); continue; }
                }

                simReactants.Add(new SimulationReactant(sidx, lidx, r.Count, r.Sign));
            }

            var sr = new SimulationRule(layerIdx, originIdx, simReactants, newIdx, fr.Probability, fr.VerboseRule) {
                VerboseRule = fr.VerboseRule
            };
            simRules.Add(sr);

            var key = (layerIdx, originIdx);
            if (!index.TryGetValue(key, out var list)) { list = new List<SimulationRule>(); index[key] = list; }
            list.Add(sr);
            layersWithRules.Add(layerIdx);
        }

        return (simRules, index, layersWithRules, warnings);
    }
}
