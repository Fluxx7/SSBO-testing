using Godot;
using Godot.Collections;


namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class UniformSet : RefCounted {
	private System.Collections.Generic.Dictionary<RenderingDevice, Rid> rids = new();
	private System.Collections.Generic.Dictionary<RenderingDevice, bool> rebuild = new();
	private System.Collections.Generic.Dictionary<uint, StringName> namedUniforms = new();
	private System.Collections.Generic.Dictionary<uint, uint> internalUniforms = new();

	~UniformSet() {
		Close();
	}

	public void Close() {
		foreach (var (rd, rid) in rids) {
			if (rd.UniformSetIsValid(rid)) {
				rd.FreeRid(rid);
			}
		}
		rids.Clear();
		rebuild.Clear();
		namedUniforms.Clear();
		foreach (var (_, uniform) in namedUniforms) {
			ShaderResourceStorage.Close(uniform);
		}
		foreach (var (_, uniform) in internalUniforms) {
			ShaderResourceStorage.Close(uniform);
		}
	}

	public void BindUniform(uint uniform_id, uint binding) {
		internalUniforms[binding] = uniform_id;
		foreach(var (rd, _) in rebuild) {
			rebuild[rd] = true;
		}
	}
	public void BindUniform(StringName uniform, uint binding) {
		namedUniforms[binding] = uniform;
		foreach(var (rd, _) in rebuild) {
			rebuild[rd] = true;
		}
	}

	public Rid GetRID(RenderingDevice rd, Rid shader, uint set_index) {
		if (!rids.ContainsKey(rd)) {
			rids[rd] = new Rid();
			rebuild[rd] = true;
		}

		bool rebuildSet = rebuild[rd] || !rd.UniformSetIsValid(rids[rd]);
		Array<RDUniform> set_uniforms = new Array<RDUniform>();
		
		// for each uniform name in the set, add an RDUniform to the array
		foreach (var (binding, rname) in namedUniforms) {
			set_uniforms.Add(ShaderResourceStorage.GetRDUniform(rd, rname, binding, out bool rebuilt));
			rebuildSet |= rebuilt;
		}
		
		foreach (var (binding, id) in internalUniforms) {
			set_uniforms.Add(ShaderResourceStorage.GetRDUniform(rd, id, binding, out bool rebuilt));
			rebuildSet |= rebuilt;
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