namespace Biome2;

public sealed class AppConfig {
	public int WindowWidth { get; init; } = 1280;
	public int WindowHeight { get; init; } = 720;
	public string WindowTitle { get; init; } = "Biome 2";
	public bool VSyncEnabled { get; init; } = true;

	// Size of a single cell in world units.
	// Rendering can treat this as pixels later if you want.
	public float CellSize { get; init; } = 1.0f;

	public static AppConfig CreateDefault() => new();
}
