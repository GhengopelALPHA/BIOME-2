namespace Biome2;

internal static class Program {
	[STAThread]
	private static void Main() {
		var config = AppConfig.CreateDefault();
		using var app = new BiomeApp(config);
		app.Run();
	}
}