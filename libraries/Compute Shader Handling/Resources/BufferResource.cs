using System;
using Godot;
using Godot.Collections;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class BufferResource(BufferResource.BufferType format, byte[] data = null)
	: ShaderResource {
	public enum BufferType {
		Uniform,
		Storage
	}

	private uint sizeBytes;
	private BufferType format = format;
	private byte[] data = data;
	private Dictionary<RenderingDevice, bool> update = new();

	public BufferResource() : this(BufferType.Uniform, null) {
		
	}

	public BufferResource(uint size_bytes, BufferType format, byte[] data = null) : this(format, data) {
		sizeBytes = format == BufferType.Storage ? size_bytes : size_bytes + size_bytes % 16;
	}
	

	public void SetData(byte[] new_data) {
		data = new_data;
		uint newSizeBytes = (format == BufferType.Storage) ? (uint) new_data.Length : (uint) new_data.Length + (uint) ( new_data.Length % 16);
		if (newSizeBytes != sizeBytes) {
			foreach (var (rd, _) in rebuild) {
				rebuild[rd] = true;
			}
		}

		sizeBytes = newSizeBytes;

		foreach (var (rd, _) in update) {
			update[rd] = true;
		}
	}
	
	public void SetData(RenderingDevice rd, byte[] new_data) {
		data = new_data;
		uint newSizeBytes = (format == BufferType.Storage) ? (uint) new_data.Length : (uint) new_data.Length + (uint) ( new_data.Length % 16);
		if (newSizeBytes != sizeBytes) {
			rebuild[rd] = true;
		}

		sizeBytes = newSizeBytes;
		update[rd] = true;
	}

	public void SetSize(uint size) {
		data = null;
		foreach (var (rd, _) in rebuild) {
			rebuild[rd] = true;
		}

		sizeBytes = size;
	}

	public byte[] GetData(RenderingDevice rd) {
		return rd.BufferGetData(rids[rd]);
	}

	public void GetDataAsync(RenderingDevice rd, Callable callback) {
		rd.BufferGetDataAsync(rids[rd], callback);
	}

	public override RDUniform GetRDUniform(RenderingDevice rd, uint binding, out bool needs_rebuild) {
		RenderingDevice.UniformType bufferType = RenderingDevice.UniformType.UniformBuffer;
		needs_rebuild = false;
		if (!rebuild.TryGetValue(rd, out bool value)) {
            value = true;
            rebuild[rd] = true;
		}
		if (value) {
			needs_rebuild = true;
			if (rids.TryGetValue(rd, out Rid rid)) {
				rd.FreeRid(rid);
			}
			if (format == BufferType.Uniform) {
				rids[rd] = data == null ? rd.UniformBufferCreate(sizeBytes) : rd.UniformBufferCreate(sizeBytes, data);
			} else {
				rids[rd] = data == null ? rd.StorageBufferCreate(sizeBytes) : rd.StorageBufferCreate(sizeBytes, data);
				bufferType = RenderingDevice.UniformType.StorageBuffer;
			}
			rebuild[rd] = false;
			update[rd] = false;
			
			rduniforms[rd] = new RDUniform {
				UniformType = bufferType
			};
			rduniforms[rd].AddId(rids[rd]);
		} else  {
			if (!update.TryGetValue(rd, out bool uval)) {
				update[rd] = false;
				uval = false;
			}

			if (uval) {
				rd.BufferUpdate(rids[rd], 0u, sizeBytes, data);
			}
			
		}
		RDUniform output = rduniforms[rd];
		output.Binding = (int) binding;
		return output;
	}
}