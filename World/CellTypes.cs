namespace Biome2.World;

/// <summary>
/// Cell data is a compact value type.
/// Keep it blittable and small for cache efficiency.
/// </summary>
public static class CellTypes {
	// Placeholder enum, later you can replace with rule defined ids.
	public enum Basic : byte {
		Empty = 0
	}
}
