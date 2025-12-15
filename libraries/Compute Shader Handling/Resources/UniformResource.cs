using Godot;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class UniformResource : ShaderResource {

	public override RDUniform GetRDUniform(RenderingDevice rd, uint binding, out bool needs_rebuild) {
		throw new System.NotImplementedException();
	}
}