using System;
using System.Collections.Generic;
using Biome2.Diagnostics;
using Biome2.FileLoading.Models;
using Biome2.World;
using static Biome2.World.CellGrid.GridTopologies;

namespace Biome2.FileLoading;

/// <summary>
/// Validate a parsed WorldModel for topology-specific constraints.
/// </summary>
public static class RulesValidator
{
    public static List<string> ValidateModel(WorldModel model) {
        var warnings = new List<string>();
        if (model == null) {
            warnings.Add("No world model provided for validation.");
            return warnings;
        }

        if (model.GridTopology == GridTopology.SPIRAL) {
            if (model.Width <= 0) warnings.Add("SPIRAL shape requires WIDTH >= 1; file provided non-positive value.");
            if (model.Height < 3) warnings.Add("SPIRAL shape requires HEIGHT >= 3 to form a valid outer ring.");
        }

        return warnings;
    }
    // Validate rules against the given world (species and layer names).
    // This validator only inspects names and returns warnings. It does not mutate
    // file models; resolution of names to indices is performed by the simulation builder.
    // Return list of warning messages encountered during validation.
    public static List<string> Validate(IReadOnlyList<RulesModel> rules, WorldState world)
    {
        var warnings = new List<string>();
        ArgumentNullException.ThrowIfNull(world);
        if (rules == null) {
            warnings.Add("There were no rules found!");
			return warnings;
		}
		ArgumentNullException.ThrowIfNull(world);

        // If the world is using Disk/Spiral topology, enforce minimum sizing constraints.
        // WIDTH maps to number of rings, HEIGHT maps to outer ring count.
        if (world != null) {
            // Note: We don't have file-level topology here; this check is conservative and
            // will not run if world appears rectangular. The caller (simulation) should
            // ensure world creation honored topology. Keep this as a warning-generator only.
        }

		for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];

            // Report unknown names; do not mutate file models.
            if (world.GetLayerIndex(rule.LayerName) < 0) warnings.Add($"{rule.VerboseRule}: unknown layer '{rule.LayerName}'");
            if (world.GetSpeciesIndex(rule.OriginSpeciesName) < 0) warnings.Add($"{rule.VerboseRule}: unknown origin species '{rule.OriginSpeciesName}'");
            if (world.GetSpeciesIndex(rule.NewSpeciesName) < 0) warnings.Add($"{rule.VerboseRule}: unknown new species '{rule.NewSpeciesName}'");
            foreach (var react in rule.Reactants) {
                if (world.GetSpeciesIndex(react.SpeciesName) < 0) warnings.Add($"{rule.VerboseRule}: reactant unknown species '{react.SpeciesName}'");
                if (!string.IsNullOrEmpty(react.LayerName) && world.GetLayerIndex(react.LayerName) < 0) warnings.Add($"{rule.VerboseRule}: reactant unknown layer '{react.LayerName}'");
                if (react.Exclusion) {
                    // Exclusionary reactants should not supply a count or sign.
                    if (react.Count != 0) warnings.Add($"{rule.VerboseRule}: exclusionary reactant should not include a count '{react.SpeciesName}'");
                    if (react.Sign != 0) warnings.Add($"{rule.VerboseRule}: exclusionary reactant should not include a trailing '+' or '-' '{react.SpeciesName}'");
                }
            }
        }

        return warnings;
    }
}
