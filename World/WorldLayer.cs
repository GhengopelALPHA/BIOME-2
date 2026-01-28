namespace Biome2.World;

/// <summary>
/// A world has multiple layers, each layer has its own cell grid.
/// Later, each layer can bind to a set of rules, display settings, and statistics.
/// </summary>
public sealed class WorldLayer {
	public string Name { get; }
	public CellGrid Grid { get; }

	public WorldLayer(string name, int widthCells, int heightCells) {
		Name = name;
		Grid = new CellGrid(widthCells, heightCells);

		Grid.FillWith([0, 1, 2]); // TODO: remove test fill
	}
}
