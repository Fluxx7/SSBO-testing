using System.Collections.Generic;
using Godot;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class TextureResource(uint x_size, uint y_size) : ShaderResource {
	private uint xSize = x_size, ySize = y_size;
	private List<Callable> callbacks = [];

	public TextureResource() : this(16, 16) {
		
	}
	
	public void SetTexture(byte[] new_data) {
	}

	public void BindTextureParameter(Callable callback) {
		callbacks.Add(callback);
	}

	public override RDUniform GetRDUniform(RenderingDevice rd, uint binding, out bool needs_rebuild) {
		needs_rebuild = false;
		if (rebuild[rd]) { 
			needs_rebuild = true;
			// 	if (rids[rd].IsValid) {
			// 		rd.FreeRid(rids[rd]);
			// 	}
			//
			// 	if (format == BufferType.Uniform) {
			// 		rids[rd] = rd.UniformBufferCreate(sizeBytes, data);
			// 	} else {
			// 		rids[rd] = rd.StorageBufferCreate(sizeBytes, data);
			// 		bufferType = RenderingDevice.UniformType.StorageBuffer;
			// 	}
			//
			// 	rebuild[rd] = false;
			//
			// 	rduniforms[rd] = new RDUniform {
			// 		UniformType = bufferType
			// 	};
			// 	rduniforms[rd].AddId(rids[rd]);
			if (rd == RenderingServer.GetRenderingDevice()) {
				foreach (Callable callback in callbacks) {
					Texture2Drd texUniform = new Texture2Drd {
						TextureRdRid = rids[rd]
					};
					callback.Call(texUniform);
				}
			}
		}
		RDUniform output = rduniforms[rd];
		output.Binding = (int) binding;
		return output;
	}
}