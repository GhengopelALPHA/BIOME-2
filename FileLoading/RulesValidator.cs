using System;
using System.Collections.Generic;
using Biome2.Diagnostics;
using Biome2.World;

namespace Biome2.FileLoading;

public static class RulesValidator
{
    // Validate rules against the given world (species and layer names).
    // Populate Resolved* fields on RulesModel and ReactantModel.
    // Return list of warning messages encountered during validation.
    public static List<string> Validate(IReadOnlyList<RulesModel> rules, WorldModel world)
    {
        var warnings = new List<string>();
        if (rules == null) {
            warnings.Add("There were no rules found!");
			return warnings;
		}
        if (world == null) throw new ArgumentNullException(nameof(world));

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];

            // Resolve layer
            rule.ResolvedLayerIndex = world.GetLayerIndex(rule.LayerName);
            if (rule.ResolvedLayerIndex < 0)
            {
                warnings.Add($"Rule #{i + 1}: unknown layer '{rule.LayerName}'");
            }

            // Resolve origin species
            rule.ResolvedOriginSpeciesIndex = world.GetSpeciesIndex(rule.OriginSpeciesName);
            if (rule.ResolvedOriginSpeciesIndex < 0)
            {
                warnings.Add($"Rule #{i + 1}: unknown origin species '{rule.OriginSpeciesName}'");
            }

            // Resolve new species
            rule.ResolvedNewSpeciesIndex = world.GetSpeciesIndex(rule.NewSpeciesName);
            if (rule.ResolvedNewSpeciesIndex < 0)
            {
                warnings.Add($"Rule #{i + 1}: unknown new species '{rule.NewSpeciesName}'");
            }

            // Resolve reactants
            foreach (var react in rule.Reactants)
            {
                react.ResolvedSpeciesIndex = world.GetSpeciesIndex(react.SpeciesName);
                if (react.ResolvedSpeciesIndex < 0)
                {
                    warnings.Add($"Rule #{i + 1}: reactant unknown species '{react.SpeciesName}'");
                }

                if (!string.IsNullOrEmpty(react.LayerName))
                {
                    react.ResolvedLayerIndex = world.GetLayerIndex(react.LayerName);
                    if (react.ResolvedLayerIndex < 0)
                    {
                        warnings.Add($"Rule #{i + 1}: reactant unknown layer '{react.LayerName}'");
                    }
                }
                else
                {
                    // Use rule's layer by default
                    react.ResolvedLayerIndex = rule.ResolvedLayerIndex;
                }
            }
        }

        return warnings;
    }
}
