using Biome2.World;
using Biome2.World.CellGrid;

namespace Biome2.World;

/// <summary>
/// A world has multiple layers, each layer has its own cell grid.
/// Later, each layer can bind to a set of rules, display settings, and statistics.
/// </summary>
public sealed class WorldLayer {
    public string Name { get; set; }
    public ICellGrid Grid { get; }

    public WorldLayer(string name, int widthCells, int heightCells, GridTopologies.GridTopology topology = GridTopologies.GridTopology.RECT) {
        Name = name;
        switch (topology) {
            case GridTopologies.GridTopology.SPIRAL:
                Grid = new DiskCellGrid(widthCells, heightCells);
                break;
            // future: case HEX -> new HexCellGrid(...)
            default:
                Grid = new RectCellGrid(widthCells, heightCells);
                break;
        }
    }
}
