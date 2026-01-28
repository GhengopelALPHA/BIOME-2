namespace Biome2.Rules;

/// <summary>
/// Placeholder for loading a rules file.
/// The UI will call into this service later (open file dialog, recent files, drag drop).
/// </summary>
public sealed class RulesLoader {
	public RuleFileRequest Load(string path) {
		// Future:
		// 1) Read file text
		// 2) Parse to AST
		// 3) Compile to simulation plan and layer rule sets
		// 4) Return a request object that the SimulationController can apply safely
		return new RuleFileRequest(path);
	}
}
