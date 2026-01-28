using OpenTK.Graphics.OpenGL4;

namespace Biome2.Graphics.GlObjects;

public sealed class VertexArrayObject : IDisposable {
	public int Handle { get; } = GL.GenVertexArray();

	public void Bind() => GL.BindVertexArray(Handle);

	public void Dispose() {
		GL.DeleteVertexArray(Handle);
	}
}
