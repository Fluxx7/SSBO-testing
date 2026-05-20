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

	private static Dictionary<uint, ResourceType> resources = new();
	private static Dictionary<uint, UniformResource> uniforms = new();
	private static Dictionary<uint, BufferResource> buffers = new();
	private static Dictionary<uint, TextureResource> textures = new();
	private static Dictionary<StringName, uint> namedResources = new();
	private static uint nextIndex = 1;

	public ShaderResourceStorage() { }
	
#region static
#region internal
	public static uint CreateInternalBuffer(RenderingDevice.UniformType type, uint size_bytes, byte[] data = null) {
		if (!CollectionExtensions.TryAdd(resources, nextIndex, ResourceType.Buffer)) {
			return 0;
		}
		buffers[nextIndex] = new BufferResource(size_bytes,
			(type == RenderingDevice.UniformType.UniformBuffer)
				? BufferResource.BufferType.Uniform
				: BufferResource.BufferType.Storage, data);
		return nextIndex++;
	}
	
	public static uint CreateInternalTexture(uint x_size, uint y_size) {
		if (!CollectionExtensions.TryAdd(resources, nextIndex, ResourceType.Texture)) {
			return 0;
		}

		textures[nextIndex] = new TextureResource(x_size, y_size);
		return nextIndex++;
	}
	
	public static uint CreateInternalTexture(uint x_size, uint y_size, TextureResource.TextureType texType) {
		if (!CollectionExtensions.TryAdd(resources, nextIndex, ResourceType.Texture)) {
			return 0;
		}

		textures[nextIndex] = new TextureResource(x_size, y_size, texType);
		return nextIndex++;
	}

	public static void SetInternalTexture(uint id, uint x_size, uint y_size, Image data) {
		textures[id].SetTexture(x_size, y_size, data);
	}
	
	public static void SetInternalTextureSize(uint id, uint x_size, uint y_size) {
		textures[id].SetSize(x_size, y_size);
	}

	public static void BindInternalTextureParameter(uint id, Callable callback) {
		textures[id].BindTextureParameter(callback);
	}

	public static void SetInternalBuffer(uint id, byte[] data = null) {
		buffers[id].SetData(data);
	}
	
	public static void SetInternalBufferSize(uint id, uint size) {
		buffers[id].SetSize(size);
	}

	public static byte[] GetInternalBufferData(uint id, RenderingDevice rd) {
		return buffers[id].GetData(rd);
	}
	
	public static void GetInternalBufferDataAsync(uint id, RenderingDevice rd, Callable callback) {
		buffers[id].GetDataAsync(rd, callback);
	}
	

	public static RDUniform GetRDUniform(RenderingDevice rd, uint id, uint binding, out bool rebuild) {
		switch (resources[id]) {
			case ResourceType.Buffer:
				return buffers[id].GetRDUniform(rd, binding, out rebuild);
			case ResourceType.Texture:
				return textures[id].GetRDUniform(rd, binding, out rebuild);
			case ResourceType.Uniform:
				return uniforms[id].GetRDUniform(rd, binding, out rebuild);
			default:
				rebuild = false;
				return new RDUniform();
		}
	}
	public static void Close(uint id) {
		switch (resources[id]) {
			case ResourceType.Buffer:
				buffers[id].Close();
				break;
			case ResourceType.Texture:
				textures[id].Close();
				break;
			case ResourceType.Uniform:
				uniforms[id].Close();
				break;
			default:
				break;
		}
	}
#endregion

#region named
	public static bool CreateBuffer(StringName buffer_name, RenderingDevice.UniformType type, uint size_bytes, byte[] data = null) {
		if (!CollectionExtensions.TryAdd(resources, nextIndex, ResourceType.Buffer)) {
			return false;
		}
		namedResources[buffer_name] = nextIndex;
		buffers[nextIndex] = new BufferResource(size_bytes,
			(type == RenderingDevice.UniformType.UniformBuffer)
				? BufferResource.BufferType.Uniform
				: BufferResource.BufferType.Storage, data);
		
		nextIndex++;
		return true;
	}
	
	public static bool CreateTexture(StringName texture_name, uint x_size, uint y_size) {
		if (!CollectionExtensions.TryAdd(resources, nextIndex, ResourceType.Texture)) {
			return false;
		}

		textures[nextIndex] = new TextureResource(x_size, y_size);
		namedResources[texture_name] = nextIndex;
		nextIndex++;
		return true;
	}
	
	public static bool CreateTexture(StringName texture_name, uint x_size, uint y_size, TextureResource.TextureType texType) {
		if (!CollectionExtensions.TryAdd(resources, nextIndex, ResourceType.Texture)) {
			return false;
		}

		textures[nextIndex] = new TextureResource(x_size, y_size, texType);
		namedResources[texture_name] = nextIndex;
		nextIndex++;
		return true;
	}

	public static void SetTexture(StringName texture_name, uint x_size, uint y_size, Image data) {
		textures[namedResources[texture_name]].SetTexture(x_size, y_size, data);
	}
	
	public static void SetTextureSize(StringName texture_name, uint x_size, uint y_size) {
		textures[namedResources[texture_name]].SetSize(x_size, y_size);
	}

	public static void BindTextureParameter(StringName texture, Callable callback) {
		textures[namedResources[texture]].BindTextureParameter(callback);
	}

	public static void SetBuffer(StringName buffer_name, byte[] data = null) {
		
		buffers[namedResources[buffer_name]].SetData(data);
	}
	
	public static void SetBufferSize(StringName buffer_name, uint size) {
		buffers[namedResources[buffer_name]].SetSize(size);
	}

	public static byte[] GetBufferData(StringName buffer_name, RenderingDevice rd) {
		return buffers[namedResources[buffer_name]].GetData(rd);
	}
	
	public static void GetBufferDataAsync(StringName buffer_name, RenderingDevice rd, Callable callback) {
		buffers[namedResources[buffer_name]].GetDataAsync(rd, callback);
	}
	

	public static RDUniform GetRDUniform(RenderingDevice rd, StringName rname, uint binding, out bool rebuild) {
		switch (resources[namedResources[rname]]) {
			case ResourceType.Buffer:
				return buffers[namedResources[rname]].GetRDUniform(rd, binding, out rebuild);
			case ResourceType.Texture:
				return textures[namedResources[rname]].GetRDUniform(rd, binding, out rebuild);
			case ResourceType.Uniform:
				return uniforms[namedResources[rname]].GetRDUniform(rd, binding, out rebuild);
			default:
				rebuild = false;
				return new RDUniform();
		}
	}
	public static void Close(StringName rname) {
		switch (resources[namedResources[rname]]) {
			case ResourceType.Buffer:
				buffers[namedResources[rname]].Close();
				break;
			case ResourceType.Texture:
				textures[namedResources[rname]].Close();
				break;
			case ResourceType.Uniform:
				uniforms[namedResources[rname]].Close();
				break;
			default:
				break;
		}
	}
#endregion
	
#endregion
}