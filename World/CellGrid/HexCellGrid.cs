using System;
using Biome2.FileLoading;
using OpenTK.Mathematics;

namespace Biome2.World.CellGrid;

/// <summary>
/// Simple hexagonal grid laid out on a rectangular backing CellGrid.
/// Logical coords: x = column, y = row. This implementation produces a
/// flat-top hex layout (points left/right) where odd columns are vertically
/// offset by half a hex height. The backing storage is a dense rectangle
/// with width = columns and height = rows; all cells are considered valid.
/// Neighboring returns 6 neighbors in the standard even-q vertical layout.
/// </summary>
public sealed class HexCellGrid : ICellGrid
{
    private readonly CellGrid _inner;
    private readonly int _cols;
    private readonly int _rows;
    public int Depth { get; }

    public HexCellGrid(int cols, int rows, int depth = 1)
    {
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
        Depth = Math.Max(1, depth);
        _inner = new CellGrid(_cols, _rows);
    }

    /// <summary>
    /// Map a world-space coordinate (renderer/world space where origin is top-left of backing grid)
    /// to a logical hex cell (col,row). Returns (-1,-1) when outside.
    /// This uses the same flat-top layout and origin used by the renderer's instance placement.
    /// </summary>
    public (int X, int Y) MapWorldToCell(Vector2 worldPos, float cellSize)
    {
        // Compute metrics
        float hexH = cellSize * 0.86602540378f; // height = sqrt(3)/2 * width
        float xStep = 0.75f * cellSize; // horizontal step between columns (3/4 width)

        // Candidate column estimate
        int estCol = (int)Math.Floor(worldPos.X / xStep);

        // search nearby candidates to find nearest cell center
        float bestDist2 = float.MaxValue;
        int bestCol = -1, bestRow = -1;

        for (int cx = estCol - 1; cx <= estCol + 1; cx++) {
            for (int ry = -1; ry <= 1; ry++) {
                int estRow = (int)Math.Floor((worldPos.Y - ((cx & 1) != 0 ? hexH * 0.5f : 0f)) / hexH);
                int row = estRow + ry;

                if (cx < 0 || cx >= _cols || row < 0 || row >= _rows) continue;

                float px = cx * xStep;
                float py = row * hexH + (((cx & 1) != 0) ? hexH * 0.5f : 0f);

                float dx = worldPos.X - px;
                float dy = worldPos.Y - py;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestDist2) {
                    bestDist2 = d2;
                    bestCol = cx; bestRow = row;
                }
            }
        }

        return (bestCol, bestRow);
    }

    public int Width => _cols;
    public int Height => _rows;

    public ReadOnlySpan<byte> CurrentSpan => _inner.CurrentSpan;
    public Span<byte> NextSpan => _inner.NextSpan;

    public int IndexOf(int x, int y) => _inner.IndexOf(x, y);

    public bool IsValidCell(int x, int y) => x >= 0 && x < _cols && y >= 0 && y < _rows && IsMaskedCell(x, y);

    private bool IsMaskedCell(int x, int y)
    {
        // Derive a hex-shaped mask based on three axis extents derived from
        // the provided WIDTH (_cols), HEIGHT (_rows) and DEPTH (Depth).
        // Convert offset coords (odd-q vertical layout) to axial then to cube
        // coordinates centered on the backing grid, then test against radii.
        if (Depth <= 0) return true;

        // radii along cube axes: map WIDTH -> rx (cube x), HEIGHT -> rz (cube z), DEPTH -> ry (cube y)
        int rx = Math.Max(0, (_cols - 1) / 2);
        int rz = Math.Max(0, (_rows - 1) / 2);
        int ry = Math.Max(0, (Depth - 1) / 2);

        // offset (odd-q) -> axial
        int aq = x;
        int ar = y - ((x - (x & 1)) / 2);

        // compute center axial coordinates for backing grid
        int centerAq = (_cols - 1) / 2;
        int centerAr = (_rows - 1) / 2 - ((centerAq - (centerAq & 1)) / 2);

        // cube coords relative to center
        int cx = aq - centerAq;
        int cz = ar - centerAr;
        int cy = -cx - cz;

        return Math.Abs(cx) <= rx && Math.Abs(cy) <= ry && Math.Abs(cz) <= rz;
    }

    public bool IsValidCellMasked(int x, int y) => IsValidCell(x, y) && IsMaskedCell(x, y);

    public byte GetCurrent(int x, int y) => IsValidCell(x, y) ? _inner.GetCurrent(x, y) : (byte)0;
    public void SetCurrent(int x, int y, byte value) { if (IsValidCell(x, y)) _inner.SetCurrent(x, y, value); }
    public void SetNext(int x, int y, byte value) { if (IsValidCell(x, y)) _inner.SetNext(x, y, value); }
    public void SwapBuffers() => _inner.SwapBuffers();
    public void CopyCurrentToNext() => _inner.CopyCurrentToNext();
    public void Clear(byte value = 0) => _inner.Clear(value);

    // neighbor ordering: N, NE, SE, S, SW, NW (then two padding entries)
    public int GetNeighbors(int x, int y, EdgeMode edgeMode, Span<byte> dest)
    {
        if (dest.Length < 8) throw new ArgumentException("dest must be at least length 8", nameof(dest));

        byte backupBorder = 255;
        byte backupInfinite = 0;
        byte backup = backupBorder;

        int ni = 0;
        if (!IsValidCell(x, y)) {
            for (int i = 0; i < 8; i++) dest[ni++] = backup;
            return ni;
        }

        bool odd = (x & 1) != 0;

        (int nx, int ny)[] neigh = odd ? new (int,int)[] {
            (x, y-1),
            (x+1, y),
            (x+1, y+1),
            (x, y+1),
            (x-1, y+1),
            (x-1, y),
        } : new (int,int)[] {
            (x, y-1),
            (x+1, y-1),
            (x+1, y),
            (x, y+1),
            (x-1, y),
            (x-1, y-1),
        };

        foreach (var pair in neigh) {
            int rx = pair.Item1;
            int ry = pair.Item2;
            bool outOfX = rx < 0 || rx >= _cols;
            bool outOfY = ry < 0 || ry >= _rows;
            bool useNeighbor = true;

            switch (edgeMode) {
                case EdgeMode.WRAP:
                    if (rx < 0) rx += _cols; else if (rx >= _cols) rx -= _cols;
                    if (ry < 0) ry += _rows; else if (ry >= _rows) ry -= _rows;
                    break;
                case EdgeMode.WRAPX:
                    if (rx < 0) rx += _cols; else if (rx >= _cols) rx -= _cols;
                    if (outOfY) useNeighbor = false;
                    break;
                case EdgeMode.WRAPY:
                    if (ry < 0) ry += _rows; else if (ry >= _rows) ry -= _rows;
                    if (outOfX) useNeighbor = false;
                    break;
                case EdgeMode.INFINITE:
                    if (outOfX || outOfY) useNeighbor = false;
                    backup = backupInfinite;
                    break;
                default:
                    if (outOfX || outOfY) useNeighbor = false;
                    break;
            }

            dest[ni++] = useNeighbor ? _inner.GetCurrent(rx, ry) : backup;
        }

        dest[ni++] = backup;
        dest[ni++] = backup;

        return ni;
    }

    public int GetNeighborCoordinates(int x, int y, EdgeMode edgeMode, Span<int> destX, Span<int> destY)
    {
        if (destX.Length < 8 || destY.Length < 8) throw new ArgumentException("destX/destY must be at least length 8");

        int ni = 0;
        if (!IsValidCell(x, y)) {
            for (int i = 0; i < 8; i++) { destX[ni] = -1; destY[ni] = -1; ni++; }
            return ni;
        }

        bool odd = (x & 1) != 0;
        (int nx, int ny)[] neigh = odd ? new (int,int)[] {
            (x, y-1),
            (x+1, y),
            (x+1, y+1),
            (x, y+1),
            (x-1, y+1),
            (x-1, y),
        } : new (int,int)[] {
            (x, y-1),
            (x+1, y-1),
            (x+1, y),
            (x, y+1),
            (x-1, y),
            (x-1, y-1),
        };

        foreach (var pair in neigh) {
            int rx = pair.Item1;
            int ry = pair.Item2;
            bool used = true;
            if (rx < 0 || rx >= _cols || ry < 0 || ry >= _rows) {
                if (edgeMode == EdgeMode.WRAP) {
                    if (rx < 0) rx = (rx % _cols + _cols) % _cols; else rx = rx % _cols;
                    if (ry < 0) ry = (ry % _rows + _rows) % _rows; else ry = ry % _rows;
                } else {
                    used = false;
                }
            }

            if (!used) { destX[ni] = -1; destY[ni] = -1; }
            else { destX[ni] = rx; destY[ni] = ry; }
            ni++;
        }

        destX[ni] = -1; destY[ni] = -1; ni++;
        destX[ni] = -1; destY[ni] = -1; ni++;

        return ni;
    }
}
