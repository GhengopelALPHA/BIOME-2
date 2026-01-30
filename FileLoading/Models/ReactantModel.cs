using System;
using System.Linq;

namespace Biome2.FileLoading.Models;

public class ReactantModel {
    // Species name as parsed from file. Will be resolved to index later.
    public string SpeciesName { get; init; } = string.Empty;

    // Optional layer name where the reactant is located. Empty means same layer as rule.
    public string LayerName { get; init; } = string.Empty;

    // Count and sign represent matching mode. Sign: +1 => >= count, -1 => <= count, 0 => == count
    public int Count { get; init; }
    public int Sign { get; init; }

    public ReactantModel() { }

    public ReactantModel(string speciesName, string layerName, int count, int sign) {
        SpeciesName = speciesName ?? string.Empty;
        LayerName = layerName ?? string.Empty;
        Count = count;
        Sign = sign;
    }


}
