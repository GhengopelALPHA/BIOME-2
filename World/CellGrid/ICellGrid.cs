
namespace Biome2.World.CellGrid;

/// <summary>
/// Minimal abstraction for a cell grid so different topologies can implement their own coordinate systems.
/// Keep it intentionally small to reduce the amount of code that needs to switch immediately.
/// Implementations should provide GetCurrent/SetNext/Swap/Copy and dimension properties.
/// Also expose lightweight accessors used by renderer/updaters: CurrentSpan, NextSpan and IndexOf.
/// </summary>
public interface ICellGrid
{
    int Width { get; }
    int Height { get; }

	// Helpful low-level access for renderer texture uploads
	ReadOnlySpan<byte> CurrentSpan { get; }
	Span<byte> NextSpan { get; }

	int IndexOf(int x, int y);

	/// <summary>
	/// Returns true when the logical coordinate (x,y) refers to a valid cell in this grid.
	/// For rectangular grids this is true for 0..Width-1,0..Height-1. For other topologies
	/// this allows sparse layouts (e.g., Disk) to indicate invalid coordinates.
	/// </summary>
	bool IsValidCell(int x, int y);

	byte GetCurrent(int x, int y);

    void SetCurrent(int x, int y, byte value);

    void SetNext(int x, int y, byte value);

    void SwapBuffers();

    void CopyCurrentToNext();

    void Clear(byte value = 0);
    /// <summary>
    /// Populate the provided span with neighbor cell values for the logical neighbors
    /// of the cell at (x,y). Returns the number of neighbors written.
    /// The ordering for rectangular grids is the 8-neighborhood in row-major order
    /// skipping the center: (-1,-1),(0,-1),(1,-1),(-1,0),(1,0),(-1,1),(0,1),(1,1).
    /// Implementations should honor the supplied EdgeMode semantics when deciding
    /// whether to use an actual neighbor value or a backup sentinel value.
    /// </summary>
    int GetNeighbors(int x, int y, EdgeMode edgeMode, Span<byte> dest);

    /// <summary>
    /// Populate the provided spans with neighbor coordinates (X and Y) using the same ordering
    /// as GetNeighbors. destX/destY must be at least length 8. Returns number of coordinates written.
    /// Invalid/out-of-range neighbors will be written as (-1,-1).
    /// </summary>
    int GetNeighborCoordinates(int x, int y, EdgeMode edgeMode, Span<int> destX, Span<int> destY);
}
