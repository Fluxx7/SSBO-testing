using System.Collections.Generic;
using Godot;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class TextureResource(uint x_size, uint y_size) : ShaderResource {
	private uint xSize = x_size, ySize = y_size;
	private Image data;
	private List<Callable> callbacks = [];
	private Dictionary<RenderingDevice, bool> update = new();

	public TextureResource() : this(16, 16) {
		
	}
	
	public void SetTexture(uint x_size, uint y_size, Image tex) {
		data = tex;
		xSize = x_size;
		ySize = y_size;
		foreach (var (rd, _) in update) {
			update[rd] = true;
		}
	}

	public void BindTextureParameter(Callable callback) {
		callbacks.Add(callback);
	}

	public override RDUniform GetRDUniform(RenderingDevice rd, uint binding, out bool needs_rebuild) {
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
			var tex_format = new RDTextureFormat() {
				Width = xSize,
				Height = ySize,
				Format = RenderingDevice.DataFormat.R16G16B16A16Sfloat,
				TextureType = RenderingDevice.TextureType.Type2D,
				UsageBits = RenderingDevice.TextureUsageBits.CanCopyFromBit | 
							RenderingDevice.TextureUsageBits.StorageBit | 
							RenderingDevice.TextureUsageBits.CanUpdateBit |
							RenderingDevice.TextureUsageBits.SamplingBit
			};
			if (data != null) {
				rids[rd] = rd.TextureCreate(tex_format, new RDTextureView(), [data.GetData()]);
			} else {
				rids[rd] = rd.TextureCreate(tex_format, new RDTextureView());
			}
			
			rebuild[rd] = false;
			update[rd] = false;
			rduniforms[rd] = new RDUniform {
				UniformType = RenderingDevice.UniformType.Image
			};
			rduniforms[rd].AddId(rids[rd]);
			if (rd == RenderingServer.GetRenderingDevice()) {
				Texture2Drd texUniform = new Texture2Drd {
					TextureRdRid = rids[rd]
				};
				foreach (Callable callback in callbacks) {
					callback.Call(texUniform);
				}
			}
		} else  {
			if (!update.TryGetValue(rd, out bool uval)) {
				update[rd] = false;
				uval = false;
			}

			if (uval) {
				rd.TextureUpdate(rids[rd], 0, data.GetData());
			}
			
		} 
		RDUniform output = rduniforms[rd];
		output.Binding = (int) binding;
		return output;
	}
}