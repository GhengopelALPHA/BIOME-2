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
        // Compute hex geometry for a flat-top layout
        // width == cellSize; height = sqrt(3)/2 * width
        float hexHeight = cellSize * 0.86602540378f; // ~= 0.8660254
        // horizontal step between column centers for flat-top hexes is 3/4 * width
        float columnStep = 0.75f * cellSize;

        // Quick column estimate based on world X. We'll search nearby columns to
        // find the true nearest hex center (since hex centers are staggered).
        int estimatedColumn = (int)Math.Floor(worldPos.X / columnStep);

        // Search nearby candidates to find the nearest cell center (squared distance)
        float bestDist2 = float.MaxValue;
        int bestCol = -1, bestRow = -1;

        // We only need to check the estimated column and its immediate neighbors
        for (int candidateColumn = estimatedColumn - 1; candidateColumn <= estimatedColumn + 1; candidateColumn++) {
            // For each column, estimate the row and probe the row and its immediate neighbors
            int estimatedRowBase = (int)Math.Floor((worldPos.Y - ((candidateColumn & 1) != 0 ? hexHeight * 0.5f : 0f)) / hexHeight);

            for (int rowOffset = -1; rowOffset <= 1; rowOffset++) {
                int candidateRow = estimatedRowBase + rowOffset;

                // Skip candidates outside the backing rectangular storage
                if (candidateColumn < 0 || candidateColumn >= _cols || candidateRow < 0 || candidateRow >= _rows) continue;

                // Compute the candidate center position in world space
                float candidateCenterX = candidateColumn * columnStep;
                float candidateCenterY = candidateRow * hexHeight + (((candidateColumn & 1) != 0) ? hexHeight * 0.5f : 0f);

                // Squared distance from worldPos to candidate center (avoid sqrt)
                float deltaX = worldPos.X - candidateCenterX;
                float deltaY = worldPos.Y - candidateCenterY;
                float distanceSq = deltaX * deltaX + deltaY * deltaY;

                if (distanceSq < bestDist2) {
                    bestDist2 = distanceSq;
                    bestCol = candidateColumn;
                    bestRow = candidateRow;
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

        // radii along cube axes: map WIDTH -> rx (cube x), HEIGHT -> rz (cube z), DEPTH -> ry (cube y)
        int rx = Math.Max(0, (_cols - 1) / 2) + 1;
        int rz = Math.Max(0, (_rows - 1) / 2) + 1;
        int ry = Math.Max(0, (Depth - 1) / 2) + 1;

        // offset (odd-q) -> axial
        // Convert from odd-q (vertical layout where odd columns are offset down)
        // to axial coordinates (q = col, r = row adjusted for offset).
        int aq = x;
        int ar = y - ((x - (x & 1)) / 2);

        // compute center axial coordinates for backing grid
        int centerAq = (_cols - 1) / 2;
        int centerAr = (_rows - 1) / 2 - ((centerAq - (centerAq & 1)) / 2);

        // cube coords relative to center
        int cx = aq - centerAq;
        int cz = ar - centerAr;
        int cy = -cx - cz;

        return Math.Abs(cx) < rx && Math.Abs(cy) < ry && Math.Abs(cz) < rz;
    }

    public bool IsValidCellMasked(int x, int y) => IsValidCell(x, y);

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

        // Values to use when a neighbor is out-of-bounds depending on edge mode
        const byte BackupBorder = 255;   // used for BORDER mode (default)
        const byte BackupInfinite = 0;   // used for INFINITE mode

        byte fillValue = BackupBorder;

        int writeIndex = 0;

        // If the target cell itself is invalid, return an array filled with the chosen backup
        if (!IsValidCell(x, y)) {
            for (int i = 0; i < 8; i++) dest[writeIndex++] = fillValue;
            return writeIndex;
        }

        bool isOddColumn = (x & 1) != 0;

        // INFINITE edges use zero as the out-of-bounds value
        if (edgeMode == EdgeMode.INFINITE) fillValue = BackupInfinite;

        // Neighbor ordering: N, NE, SE, S, SW, NW
        (int cx, int cy)[] neighbors = isOddColumn ? new (int,int)[] {
            (x, y-1),    // N
            (x+1, y),    // NE
            (x+1, y+1),  // SE
            (x, y+1),    // S
            (x-1, y+1),  // SW
            (x-1, y),    // NW
        } : new (int,int)[] {
            (x, y-1),    // N
            (x+1, y-1),  // NE
            (x+1, y),    // SE
            (x, y+1),    // S
            (x-1, y),    // SW
            (x-1, y-1),  // NW
        };

        // Helper: test a candidate coordinate both inside rectangular backing storage
        // and inside the hex mask (so that masked-off corners are treated as empty).
        bool IsCandidateValid(int candX, int candY) => candX >= 0 && candX < _cols && candY >= 0 && candY < _rows && IsMaskedCell(candX, candY);

        foreach (var neighbor in neighbors) {
            int candidateX = neighbor.cx;
            int candidateY = neighbor.cy;

            // Default: assume neighbor not found and will use the fillValue
            bool foundNeighbor = false;
            int selectedX = -1, selectedY = -1;

            // If the immediate neighbor is valid, use it directly
            if (IsCandidateValid(candidateX, candidateY)) {
                selectedX = candidateX; selectedY = candidateY; foundNeighbor = true;
            } else {
                // For wrapping topologies, allow searching nearby tiled copies so
                // edges line up correctly with the hex mask. All WRAP variants
                // (WRAP, WRAPX, WRAPY) behave the same for this implementation.
                bool allowWrap = edgeMode == EdgeMode.WRAP || edgeMode == EdgeMode.WRAPX || edgeMode == EdgeMode.WRAPY;

                if (allowWrap) {
                    // Search offsets (-1,0,1) in both backing axes. Prioritize
                    // tiles by increasing Manhattan distance so we choose the
                    // nearest wrapped copy.
                    int[] offsetRange = new int[] { -1, 0, 1 };

                    for (int manhattanDistance = 0; manhattanDistance <= 2 && !foundNeighbor; manhattanDistance++) {
                        foreach (int offsetX in offsetRange) {
                            foreach (int offsetY in offsetRange) {
                                if (Math.Abs(offsetX) + Math.Abs(offsetY) != manhattanDistance) continue;
                                int wrappedX = candidateX + offsetX * _cols;
                                int wrappedY = candidateY + offsetY * _rows;
                                if (IsCandidateValid(wrappedX, wrappedY)) {
                                    selectedX = wrappedX; selectedY = wrappedY; foundNeighbor = true; break;
                                }
                            }
                            if (foundNeighbor) break;
                        }
                    }
                }
                // If wrapping wasn't allowed or nothing matched, foundNeighbor remains false
            }

            // Write either the neighbor value or the chosen fill value
            dest[writeIndex++] = foundNeighbor ? _inner.GetCurrent(selectedX, selectedY) : fillValue;
        }

        // Two padding entries (kept for compatibility with callers expecting 8 entries)
        //dest[writeIndex++] = fillValue;
        //dest[writeIndex++] = fillValue;

        return writeIndex;
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

        // Reuse much of the logic from GetNeighbors but record coordinates instead
        bool IsCandidateValid(int cx, int cy) => cx >= 0 && cx < _cols && cy >= 0 && cy < _rows && IsMaskedCell(cx, cy);

        foreach (var pair in neigh) {
            int neighborX = pair.Item1;
            int neighborY = pair.Item2;

            bool used = false;
            int selectedX = -1, selectedY = -1;

            if (IsCandidateValid(neighborX, neighborY)) {
                selectedX = neighborX; selectedY = neighborY; used = true;
            } else {
                bool allowWrap = edgeMode == EdgeMode.WRAP || edgeMode == EdgeMode.WRAPX || edgeMode == EdgeMode.WRAPY;

                if (allowWrap) {
                    int[] offsetRangeX = new int[] { -1, 0, 1 };
                    int[] offsetRangeY = new int[] { -1, 0, 1 };

                    for (int manhattanDistance = 0; manhattanDistance <= 2 && !used; manhattanDistance++) {
                        foreach (int offsetX in offsetRangeX) {
                            foreach (int offsetY in offsetRangeY) {
                                if (Math.Abs(offsetX) + Math.Abs(offsetY) != manhattanDistance) continue;
                                int cx = neighborX + offsetX * _cols;
                                int cy = neighborY + offsetY * _rows;
                                if (IsCandidateValid(cx, cy)) { selectedX = cx; selectedY = cy; used = true; break; }
                            }
                            if (used) break;
                        }
                    }
                }
            }

            if (!used) { destX[ni] = -1; destY[ni] = -1; }
            else { destX[ni] = selectedX; destY[ni] = selectedY; }
            ni++;
        }

        // Two padding entries at the end for callers expecting 8 entries
        destX[ni] = -1; destY[ni] = -1; ni++;
        destX[ni] = -1; destY[ni] = -1; ni++;

        return ni;
    }
}
