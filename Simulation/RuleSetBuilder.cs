using Biome2.Diagnostics;
using Biome2.FileLoading.Models;
using Biome2.Simulation.Models;
using Biome2.World;

namespace Biome2.Simulation;

/// <summary>
/// Converts file-loading models into simulation-ready structures.
/// </summary>
public static class RuleSetBuilder {
    public static (List<SimulationRuleModel> rules, Dictionary<(int layer, int origin), List<SimulationRuleModel>> index, HashSet<int> layersWithRules)
        Build(IReadOnlyList<RulesModel> fileRules, WorldState world)
    {
        var simRules = new List<SimulationRuleModel>();
        var index = new Dictionary<(int layer, int origin), List<SimulationRuleModel>>();
        var layersWithRules = new HashSet<int>();

        if (fileRules == null) return (simRules, index, layersWithRules);

        for (int i = 0; i < fileRules.Count; i++) {
            var fr = fileRules[i];

            int layerIdx = world.GetLayerIndex(fr.LayerName);
            if (layerIdx < 0)
                continue;

            int originIdx = world.GetSpeciesIndex(fr.OriginSpeciesName);
            if (originIdx < 0)
                continue;

            int newIdx = world.GetSpeciesIndex(fr.NewSpeciesName);
            if (newIdx < 0)
                continue;


            // Move metadata resolution: if the file specified a move operation, resolve mover species to index
            int moveSpeciesIdx = -1;
            if (!string.IsNullOrEmpty(fr.MoveSpeciesName)) {
                moveSpeciesIdx = world.GetSpeciesIndex(fr.MoveSpeciesName);
            }

			if (newIdx == originIdx && moveSpeciesIdx == -1) 
                continue;

			var simReactants = new List<SimulationReactantModel>();
            foreach (var r in fr.Reactants) {
                int sidx = world.GetSpeciesIndex(r.SpeciesName);
                if (sidx < 0)
                    continue;

                int lidx;
                if (string.IsNullOrEmpty(r.LayerName)) {
                    // No explicit layer specified in the reactant: use -1 to indicate "use neighborhood on the rule's layer".
                    lidx = -1;
                } else {
                    lidx = world.GetLayerIndex(r.LayerName);
                    if (lidx < 0)
                        continue;
                }

                // Exclusionary reactants are allowed and were parsed by the loader.
                // Propagate the flag to the simulation reactant and validate
                // incompatible combinations (count or sign with exclusion should
                // have been rejected by the loader already).
                simReactants.Add(new SimulationReactantModel(sidx, lidx, r.Count, r.Sign, r.Exclusion));
            }

            var sr = new SimulationRuleModel(
                layerIdx,
                originIdx,
                simReactants,
                newIdx,
                fr.Probability,
                fr.VerboseRule,
                fr.XMin,
                fr.XMax,
                fr.YMin,
                fr.YMax,
                moveSpeciesIdx
            );

            simRules.Add(sr);

            var key = (layerIdx, originIdx);
            if (!index.TryGetValue(key, out var list)) { list = []; index[key] = list; }
            list.Add(sr);
            layersWithRules.Add(layerIdx);
        }

		return (simRules, index, layersWithRules);
    }
}
