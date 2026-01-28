using OpenTK.Graphics.OpenGL4;

namespace Biome2.Graphics.GLObjects;

public sealed class ShaderProgram : IDisposable {
	public int Handle { get; }

	public ShaderProgram(string vertexSource, string fragmentSource) {
		int vert = CompileShader(ShaderType.VertexShader, vertexSource);
		int frag = CompileShader(ShaderType.FragmentShader, fragmentSource);

		Handle = GL.CreateProgram();
		GL.AttachShader(Handle, vert);
		GL.AttachShader(Handle, frag);
		GL.LinkProgram(Handle);

		GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int status);
		if (status == 0) {
			string info = GL.GetProgramInfoLog(Handle);
			throw new InvalidOperationException($"Shader link failed: {info}");
		}

		GL.DetachShader(Handle, vert);
		GL.DetachShader(Handle, frag);
		GL.DeleteShader(vert);
		GL.DeleteShader(frag);
	}

	public void Use() => GL.UseProgram(Handle);

	public int GetUniformLocation(string name) => GL.GetUniformLocation(Handle, name);

	public void Dispose() {
		GL.DeleteProgram(Handle);
	}

	private static int CompileShader(ShaderType type, string src) {
		int shader = GL.CreateShader(type);
		GL.ShaderSource(shader, src);
		GL.CompileShader(shader);

		GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
		if (status == 0) {
			string info = GL.GetShaderInfoLog(shader);
			throw new InvalidOperationException($"{type} compile failed: {info}");
		}

		return shader;
	}
}
