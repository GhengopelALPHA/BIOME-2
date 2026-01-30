using System;

namespace Biome2.Simulation.Models;

/// <summary>
/// Simulation-side reactant representation with resolved indices ready for fast checks.
/// </summary>
public sealed class SimulationReactant {
    public int SpeciesIndex { get; }
    public int LayerIndex { get; }
    public int Count { get; }
    public int Sign { get; }

    public SimulationReactant(int speciesIndex, int layerIndex, int count, int sign) {
        SpeciesIndex = speciesIndex;
        LayerIndex = layerIndex;
        Count = count;
        Sign = sign;
    }

    public bool Check(ReadOnlySpan<byte> neighbors) {
        if (SpeciesIndex < 0) return false;
        int speciesCount = 0;
        for (int i = 0; i < neighbors.Length; i++) {
            if (neighbors[i] == SpeciesIndex) speciesCount++;
        }
        if (Sign == 1) return speciesCount >= Count;
        if (Sign == -1) return speciesCount <= Count;
        return speciesCount == Count;
    }
}
