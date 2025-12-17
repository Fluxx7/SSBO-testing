using System.Collections.Generic;
using Godot;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public abstract partial class ShaderResource : RefCounted {
	protected Dictionary<RenderingDevice, Rid> rids = new();
	protected Dictionary<RenderingDevice, RDUniform> rduniforms = new();
	protected Dictionary<RenderingDevice, bool> rebuild = new();

	~ShaderResource() {
		foreach (var (rd, rid) in rids) {
			rd.FreeRid(rid);
		}
	}

	public void Close() {
		foreach (var (rd, rid) in rids) {
			rd.FreeRid(rid);
		}
	}
	
	public abstract RDUniform GetRDUniform(RenderingDevice rd, uint binding, out bool needs_rebuild);

}