using OpenTK.Mathematics;

namespace Biome2.Rules;

public sealed class SpeciesModel {
	private string _name = string.Empty;
	public string Name => _name;

	private Color4 _color;
	public Color4 Color => _color;

	// future attribute system

	public SpeciesModel() { }

	public SpeciesModel(string name, Color4 color) {
		_name = name;
		_color = color;
	}
}
