namespace Biome2.World;

/// <summary>
/// Optional snapshot format for history, debugging, and replay.
/// Keep this separate from the live WorldModel so it can be serialized efficiently.
/// </summary>
public sealed class WorldSnapshot {
	public int WidthCells { get; init; }
	public int HeightCells { get; init; }

	// One entry per layer, each a flat array of cell values.
	public List<byte[]> LayerCellData { get; init; } = new();
}
