using System;
using System.Linq;

namespace Biome2.FileLoading;

public class ReactantModel {
    // Species name as parsed from file. Will be resolved to index later.
    public string SpeciesName { get; init; } = string.Empty;

    // Optional layer name where the reactant is located. Empty means same layer as rule.
    public string LayerName { get; init; } = string.Empty;

    // Count and sign represent matching mode. Sign: +1 => >= count, -1 => <= count, 0 => == count
    public int Count { get; init; }
    public int Sign { get; init; }

    // Resolved species index (populated during rule validation). -1 when unresolved.
    public int ResolvedSpeciesIndex { get; set; } = -1;
    // Resolved layer index (populated during rule validation). -1 means use rule's layer.
    public int ResolvedLayerIndex { get; set; } = -1;

    public ReactantModel() { }

    public ReactantModel(string speciesName, string layerName, int count, int sign) {
        SpeciesName = speciesName ?? string.Empty;
        LayerName = layerName ?? string.Empty;
        Count = count;
        Sign = sign;
    }

    // Check against an array/span of neighbor species indices (bytes).
    // Expects ResolvedSpeciesIndex to be set to a valid species index prior to calling.
    public bool Check(ReadOnlySpan<byte> neighbors) {
        if (ResolvedSpeciesIndex < 0) return false;

        int speciesCount = 0;
        for (int i = 0; i < neighbors.Length; i++) {
            if (neighbors[i] == (byte)ResolvedSpeciesIndex) speciesCount++;
        }

        if (Sign == 1) {
            return speciesCount >= Count;
        } else if (Sign == -1) {
            return speciesCount <= Count;
        } else {
            return speciesCount == Count;
        }
    }
}
