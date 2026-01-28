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

	public void NewSpecies(string name, Color4 color) {
		_name = name;
		_color = color;
	}

	/// <summary>
	/// Convert the stored Color4 (components in 0..1) to RGBA8 bytes.
	/// </summary>
	public byte[] ToRgbaBytes() {
		static byte Conv(float v) =>
			(byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

		return new byte[] {
			Conv(_color.R),
			Conv(_color.G),
			Conv(_color.B),
			Conv(_color.A)
		};
	}
}
