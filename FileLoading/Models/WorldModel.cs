using Biome2.World.CellGrid;

namespace Biome2.FileLoading.Models;

/// <summary>
/// A safe transport object representing a loaded world definition from a rules file.
/// This carries parsed species, layer names, rules, and basic settings but does not
/// contain any resolved indexes. It is intended for file-loading only.
/// </summary>
public sealed class WorldModel {
    // Parsed settings
    public WorldConfigModel Config { get; init; }

	// Species definitions (name -> color)
	public IReadOnlyList<SpeciesModel> Species { get; init; } = [];

    // Layer names in order
    public IReadOnlyList<string> Layers { get; init; } = [];

    // Parsed rules
    public IReadOnlyList<RulesModel> Rules { get; init; } = [];
    

    public WorldModel(
        WorldConfigModel config,
		IReadOnlyList<SpeciesModel> species,
        IReadOnlyList<string> layers,
        IReadOnlyList<RulesModel> rules
	) {
        Config = config;
        Species = species ?? [];
        Layers = layers ?? [];
        Rules = rules ?? [];
	}
}
