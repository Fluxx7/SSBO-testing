using Godot;
using Godot.Collections;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class UniformSet : RefCounted {
	private Dictionary<RenderingDevice, Rid> rids = new();
	private Dictionary<RenderingDevice, bool> rebuild = new();
	private Dictionary<uint, StringName> uniforms = new();

	~UniformSet() {
		foreach (var (rd, rid) in rids) {
			rd.FreeRid(rid);
		}
	}

	public void BindUniform(StringName uniform, uint binding) {
		uniforms[binding] = uniform;
		foreach(var (rd, _) in rebuild) {
			rebuild[rd] = true;
		}
	}

	public Rid GetRID(RenderingDevice rd, Rid shader, uint set_index) {
		if (!rids.ContainsKey(rd)) {
			rids[rd] = new Rid();
			rebuild[rd] = true;
		}

		bool rebuildSet = rebuild[rd];
		Array<RDUniform> set_uniforms = new Array<RDUniform>();
		
		// for each uniform name in the set, add an RDUniform to the array
		foreach (var (binding, rname) in uniforms) {
			set_uniforms.Add(ShaderResourceStorage.GetRDUniform(rd, rname, binding, out rebuildSet));
		}
		
		// TODO: determine what else would need the set to be rebuilt
		if (rebuildSet) {
			if (rd.UniformSetIsValid(rids[rd])) {
				rd.FreeRid(rids[rd]);
			}
			rids[rd] = rd.UniformSetCreate(set_uniforms, shader, set_index);
			rebuild[rd] = false;
		}
		return rids[rd];
	}
	
}