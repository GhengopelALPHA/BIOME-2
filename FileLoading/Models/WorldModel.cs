namespace Biome2.FileLoading.Models;

/// <summary>
/// A safe transport object representing a loaded world definition from a rules file.
/// This carries parsed species, layer names, rules, and basic settings but does not
/// contain any resolved indexes. It is intended for file-loading only.
/// </summary>
public sealed class WorldModel {
    // Parsed settings
    public int Width { get; init; }
    public int Height { get; init; }
    public bool Paused { get; init; }

    // Species definitions (name -> color)
    public IReadOnlyList<SpeciesModel> Species { get; init; } = Array.Empty<SpeciesModel>();

    // Layer names in order
    public IReadOnlyList<string> Layers { get; init; } = Array.Empty<string>();

    // Parsed rules
    public IReadOnlyList<RulesModel> Rules { get; init; } = Array.Empty<RulesModel>();
    
    // Edge handling mode for neighbor queries
    public EdgeMode Edges { get; init; } = EdgeMode.BORDER;

    public WorldModel(
        int width,
        int height,
        bool paused,
        IReadOnlyList<SpeciesModel> species,
        IReadOnlyList<string> layers,
        IReadOnlyList<RulesModel> rules,
        EdgeMode edges = EdgeMode.BORDER
    ) {
        Width = width;
        Height = height;
        Paused = paused;
        Species = species ?? Array.Empty<SpeciesModel>();
        Layers = layers ?? Array.Empty<string>();
        Rules = rules ?? Array.Empty<RulesModel>();
        Edges = edges;
    }
}
