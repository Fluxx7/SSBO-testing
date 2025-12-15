using Godot;
using Godot.Collections;
using CollectionExtensions = System.Collections.Generic.CollectionExtensions;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class ShaderResourceStorage : RefCounted {
	enum ResourceType {
		Uniform, 
		Buffer,
		Texture
	};

	private static Dictionary<StringName, ResourceType> resources = new();
	private static Dictionary<StringName, UniformResource> uniforms = new();
	private static Dictionary<StringName, BufferResource> buffers = new();
	private static Dictionary<StringName, TextureResource> textures = new();

	public ShaderResourceStorage() { }
	
#region static
	public static bool CreateBuffer(StringName buffer_name, RenderingDevice.UniformType type, uint size_bytes, byte[] data = null) {
		if (!CollectionExtensions.TryAdd(resources, buffer_name, ResourceType.Buffer)) {
			return false;
		}

		buffers[buffer_name] = new BufferResource(size_bytes,
			(type == RenderingDevice.UniformType.UniformBuffer)
				? BufferResource.BufferType.Uniform
				: BufferResource.BufferType.Storage, data);
		return true;
	}
	
	public static bool CreateTexture(StringName texture_name, uint x_size, uint y_size) {
		if (!CollectionExtensions.TryAdd(resources, texture_name, ResourceType.Texture)) {
			return false;
		}

		textures[texture_name] = new TextureResource(x_size, y_size);
		return true;
	}

	public static void BindTextureParameter(StringName texture, Callable callback) {
		textures[texture].BindTextureParameter(callback);
	}

	public static void SetBuffer(StringName buffer_name, byte[] data = null) {
		buffers[buffer_name].SetData(data);
	}

	public static byte[] GetBufferData(StringName buffer_name, RenderingDevice rd) {
		return buffers[buffer_name].GetData(rd);
	}


	

	public static RDUniform GetRDUniform(RenderingDevice rd, StringName rname, uint binding, out bool rebuild) {
		switch (resources[rname]) {
			case ResourceType.Buffer:
				return buffers[rname].GetRDUniform(rd, binding, out rebuild);
			case ResourceType.Texture:
				return textures[rname].GetRDUniform(rd, binding, out rebuild);
			case ResourceType.Uniform:
				return uniforms[rname].GetRDUniform(rd, binding, out rebuild);
			default:
				rebuild = false;
				return new RDUniform();
		}
	}
#endregion
}