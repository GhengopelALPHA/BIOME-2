namespace Biome2.FileLoading;

/// <summary>
/// A safe transport object representing a load request.
/// Later this can include parsed AST, compiled opcode blobs, metadata, and validation messages.
/// </summary>
public sealed class RulesFileRequest {
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

    public RulesFileRequest(
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
