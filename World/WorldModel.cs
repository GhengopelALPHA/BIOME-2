
using Biome2.Diagnostics;

namespace Biome2.World;

/// <summary>
/// The root world container.
/// Keeps layers, dimensions, metadata, and hooks for history and statistics.
/// </summary>
public sealed class WorldModel {
	// World defaults for first launch.
	private const int DefaultWorldWidthCells = 256;
	private const int DefaultWorldHeightCells = 256;
	private const int DefaultWorldLayerCount = 1;

	public int WidthCells { get; }
	public int HeightCells { get; }
	public int LayerCount { get; }

	private readonly List<WorldLayer> _layers = new();
	public IReadOnlyList<WorldLayer> Layers => _layers;

	// Layer viewing can swap which layer is currently visible.
	public int ActiveLayerIndex { get; set; } = 0;

	public WorldLayer ActiveLayer => _layers[ActiveLayerIndex];

	private WorldModel(int widthCells, int heightCells, int layerCount) {
		// bound checking
		var _widthCells = widthCells;
		var _heightCells = heightCells;
		var _layerCount = layerCount;

		if (widthCells <= 0) {
			Logger.Error("WidthCells must be positive.");
			_widthCells = 1;
		}
		if (heightCells <= 0) {
			Logger.Error("HeightCells must be positive.");
			_heightCells = 1;
		}
		if (layerCount <= 0) {
			Logger.Error("LayerCount must be positive.");
			_layerCount = 1;
		}

		WidthCells = _widthCells;
		HeightCells = _heightCells;
		LayerCount = _layerCount;

		CreateLayers();
	}

	private void CreateLayers() {
		for (int i = 0; i < LayerCount; i++) {
			_layers.Add(new WorldLayer($"Layer {i}", WidthCells, HeightCells));
		}
	}

	public static WorldModel CreateBlank() {
		return new WorldModel(
			DefaultWorldWidthCells,
			DefaultWorldHeightCells,
			DefaultWorldLayerCount
		);
	}
}
