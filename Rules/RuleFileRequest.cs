namespace Biome2.Rules;

/// <summary>
/// A safe transport object representing a load request.
/// Later this can include parsed AST, compiled opcode blobs, metadata, and validation messages.
/// </summary>
public sealed class RuleFileRequest {
	public string Path { get; }

	public RuleFileRequest(string path) {
		Path = path;
	}
}
