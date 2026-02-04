using System;
using System.Linq;

namespace Biome2.FileLoading.Models;

public class ReactantModel(
    string speciesName,
    string layerName,
    int count,
    int sign,
    bool exclusion
) {
	// Species name as parsed from file. Will be resolved to index later.
	public string SpeciesName { get; init; } = speciesName ?? string.Empty;

	// Optional layer name where the reactant is located. Empty means same layer as rule.
	public string LayerName { get; init; } = layerName ?? string.Empty;

	// Count and sign represent matching mode. Sign: +1 => >= count, -1 => <= count, 0 => == count
	public int Count { get; init; } = count;
	public int Sign { get; init; } = sign;
    // If true, this reactant is exclusionary: the rule matches only if the species
    // is NOT present on the target layer. Does not apply for neighborhood checks
    // (This is already possible with "0species" reactant rules)
    public bool Exclusion { get; init; } = exclusion;
}
