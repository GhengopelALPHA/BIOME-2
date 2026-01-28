using OpenTK.Graphics.OpenGL4;

namespace Biome2.Graphics.GlObjects;

public sealed class BufferObject : IDisposable {
	public int Handle { get; }
	private readonly BufferTarget _target;

	public BufferObject(BufferTarget target) {
		_target = target;
		Handle = GL.GenBuffer();
	}

	public void Bind() => GL.BindBuffer(_target, Handle);

	public void SetData<T>(ReadOnlySpan<T> data, BufferUsageHint usage) where T : unmanaged {
		Bind();
		GL.BufferData(_target, data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>(), data.ToArray(), usage);
	}

	public void Dispose() {
		GL.DeleteBuffer(Handle);
	}
}
