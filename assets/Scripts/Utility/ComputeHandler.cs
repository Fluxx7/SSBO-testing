using Godot;
using Godot.Collections;

namespace GraphicsTesting.assets.Scripts.Utility;

public partial class ComputeHandler : GodotObject {
	private RenderingDevice rd;
	
	// Values tied to each shader
	private Dictionary<StringName, Rid> shaders = new();
	private Dictionary<StringName, Rid> pipelines = new();
	private Dictionary<StringName, Array<Array<StringName>>> shaderUniforms = new();
	private Dictionary<StringName, Array<Rid>> uniformSets = new();
	private Dictionary<StringName, Array<Array<Rid>>> uniformSetUniforms = new();
	
	// Values tied to uniforms
	private Dictionary<StringName, Rid> uniforms = new();
	private Dictionary<StringName, RenderingDevice.UniformType> uniformTypes = new();
	private Dictionary<StringName, int> uniformSizes = new();

	public ComputeHandler() {
		rd = RenderingServer.CreateLocalRenderingDevice();
	}
	
	public ComputeHandler(RenderingDevice new_rd) {
		rd = new_rd;
	}

	~ComputeHandler() {
		foreach (var pipeline in pipelines) {
			if (rd.ComputePipelineIsValid(pipeline.Value)) {
				rd.FreeRid(pipeline.Value);
			}

			rd.FreeRid(shaders[pipeline.Key]);
		}
		foreach (var uniformSetArray in uniformSets) {
			foreach (var uniformSet in uniformSetArray.Value) {
				if (rd.UniformSetIsValid(uniformSet)) {
					rd.FreeRid(uniformSet);
				}
			}
		}
		foreach (var pipeline in pipelines) {
			if (rd.ComputePipelineIsValid(pipeline.Value)) {
				rd.FreeRid(pipeline.Value);
			}
		}
		foreach (var uniform in uniforms) {
			rd.FreeRid(uniform.Value);
		}
	}

	public void Close() {
		foreach (var pipeline in pipelines) {
			if (rd.ComputePipelineIsValid(pipeline.Value)) {
				rd.FreeRid(pipeline.Value);
			}
			rd.FreeRid(shaders[pipeline.Key]);
		}
		pipelines.Clear();
		shaders.Clear();
		foreach (var uniformSetArray in uniformSets) {
			foreach (var uniformSet in uniformSetArray.Value) {
				if (rd.UniformSetIsValid(uniformSet)) {
					rd.FreeRid(uniformSet);
				}
			}
			uniformSetArray.Value.Clear();
		}
		uniformSets.Clear();
		foreach (var uniform in uniforms) {
			rd.FreeRid(uniform.Value);
		}
		uniforms.Clear();
		uniformSizes.Clear();
	}

	#region shader
	public void AddShader(StringName shader, RDShaderFile shaderFile) {
		if (pipelines.TryGetValue(shader, out var oldPipeline)) {
			if (rd.ComputePipelineIsValid(oldPipeline)) {
				rd.FreeRid(oldPipeline);
			}
			rd.FreeRid(shaders[shader]);
			foreach (var set in uniformSets[shader]) {
				if (rd.UniformSetIsValid(set)) {
					rd.FreeRid(set);
				}
			}
		}

		shaders[shader] = rd.ShaderCreateFromSpirV(shaderFile.GetSpirV());
		pipelines[shader] = rd.ComputePipelineCreate(shaders[shader]);
		if (!shaderUniforms.ContainsKey(shader)) {
			shaderUniforms[shader] = new Array<Array<StringName>>();
		}
		uniformSets[shader] = new Array<Rid>(); 
		uniformSetUniforms[shader] = new Array<Array<Rid>>();
		
	}

	public bool HasShader(StringName shader) {
		return shaders.ContainsKey(shader);
	}
	
	public void Dispatch(StringName shader, uint xThreads, uint yThreads, uint zThreads, byte[] push_constants = null) {
		uint currSet = 0;
		
		// for each set of uniform names stored for this shader
		foreach (var curr_arr in shaderUniforms[shader]) {
			bool rebuildSet = false;
			if (uniformSetUniforms[shader].Count < currSet + 1) {
				uniformSetUniforms[shader].Resize((int) currSet + 1);
				rebuildSet = true;
			}
			Array<RDUniform> set_uniforms = new Array<RDUniform>();
			int binding = 0;
			// for each uniform name in the set, add an RDUniform to the array
			foreach (var uniform in curr_arr) {
				Rid uni_id = uniforms[uniform];
				if (uniformSetUniforms[shader][(int)currSet].Count < binding + 1) {
					uniformSetUniforms[shader][(int)currSet].Resize(binding + 1);
					rebuildSet = true;
				} else if (uni_id != uniformSetUniforms[shader][(int)currSet][binding]) {
					if (uniformSetUniforms[shader][(int)currSet][binding].IsValid) {
						rd.FreeRid(uniformSetUniforms[shader][(int)currSet][binding]);
					}
					rebuildSet = true;
				}

				var new_uniform = new RDUniform() {
					UniformType = uniformTypes[uniform],
					Binding = binding
				};
				new_uniform.AddId(uni_id);
				set_uniforms.Add(new_uniform);
				uniformSetUniforms[shader][(int)currSet][binding] = uni_id;
				binding++;
			}

			if (rebuildSet) {
				if (uniformSets[shader].Count < (int)currSet) {
					uniformSets[shader].Resize((int)currSet);
				} else if (rd.UniformSetIsValid(uniformSets[shader][(int)currSet])) {
					rd.FreeRid(uniformSets[shader][(int)currSet]);
				}

				uniformSets[shader][(int)currSet] = rd.UniformSetCreate(set_uniforms, shaders[shader], currSet);
			}
			currSet++;
		}
		
		var computeList = rd.ComputeListBegin();
		rd.ComputeListBindComputePipeline(computeList, pipelines[shader]);
		if (push_constants != null) {
			rd.ComputeListSetPushConstant(computeList, push_constants, (uint) push_constants.Length);
		}
		currSet = 0;
		foreach (var set in uniformSets[shader]) {
			rd.ComputeListBindUniformSet(computeList, set, currSet);
			currSet++;
		}
		rd.ComputeListDispatch(computeList, xThreads, yThreads, zThreads);
		rd.ComputeListEnd();
	}
	
	public void DispatchSubmit(StringName shader, uint xThreads, uint yThreads, uint zThreads, byte[] push_constants = null) {
		Dispatch(shader, xThreads, yThreads, zThreads, push_constants);
		rd.Submit();
	}

	public void Sync() {
		rd.Sync();
	}
	
#endregion
	
#region uniforms
	public Rid CreateBuffer(StringName buffer, RenderingDevice.UniformType type, uint sizeBytes, byte[] data = null) {
		if (uniforms.TryGetValue(buffer, out Rid old_rid)) {
			rd.FreeRid(old_rid);
		}

		Rid new_rid;
		if (type == RenderingDevice.UniformType.UniformBuffer) {
			if (data != null) {
				new_rid = rd.UniformBufferCreate(sizeBytes, data);
			} else {
				new_rid = rd.UniformBufferCreate(sizeBytes);
			}
			uniformTypes[buffer] = RenderingDevice.UniformType.UniformBuffer;
		} else if (type == RenderingDevice.UniformType.StorageBuffer) {
			if (data != null) {
				new_rid = rd.StorageBufferCreate(sizeBytes, data);
			} else {
				new_rid = rd.StorageBufferCreate(sizeBytes);
			}
			uniformTypes[buffer] = RenderingDevice.UniformType.StorageBuffer;
		} else {
			return new Rid();
		}
		uniforms[buffer] = new_rid;
		uniformSizes[buffer] = (int) sizeBytes;
		return new_rid;
	}
	
	public Rid CreateBuffer(StringName buffer, RenderingDevice.UniformType type, uint sizeBytes, StringName shader, int set, int binding, byte[] data = null) {
		Rid new_rid = CreateBuffer(buffer, type, sizeBytes, data);
		AssignUniform(shader, buffer, set, binding);
		return new_rid;
	}

	public Rid CreateTexture(StringName texture, uint xSize, uint ySize) {
		if (uniforms.TryGetValue(texture, out Rid old_rid)) {
			rd.FreeRid(old_rid);
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
		Rid tex_rid = rd.TextureCreate(tex_format, new RDTextureView());
		uniforms[texture] = tex_rid;
		uniformTypes[texture] = RenderingDevice.UniformType.Image;
		return tex_rid;
	}

	

	public void AssignUniform(StringName shader, StringName buffer, int set, int binding) {
		if (shaderUniforms[shader].Count < set + 1) {
			shaderUniforms[shader].Resize(set + 1);
		}
		if (shaderUniforms[shader][set].Count < binding + 1) {
			shaderUniforms[shader][set].Resize(binding + 1);
		}

		if (!uniforms.ContainsKey(buffer)) {
			GD.Print("Buffer " + buffer + " does not exist");
			return;
		}
		shaderUniforms[shader][set][binding] = buffer;
		if (uniformSets[shader].Count < set + 1) {
			uniformSets[shader].Resize(set + 1);
		}
	}
	
	public Rid SetBuffer(StringName buffer, uint sizeBytes, byte[] values = null) {
		bool isUniform = uniformTypes[buffer] == RenderingDevice.UniformType.UniformBuffer;
		uint trueSizeBytes = isUniform ? sizeBytes + 16 - (sizeBytes % 16) : sizeBytes;
		if (trueSizeBytes != uniformSizes[buffer]) {
			if (uniforms[buffer].IsValid) {
				rd.FreeRid(uniforms[buffer]);
			}

			Rid newbuf;

			if (isUniform) {
				if (values != null) {
					newbuf = rd.UniformBufferCreate(trueSizeBytes, values);
				} else {
					newbuf = rd.UniformBufferCreate(trueSizeBytes);
				}

				uniformSizes[buffer] = (int)trueSizeBytes;
			} else {
				if (values != null) {
					newbuf = rd.StorageBufferCreate(sizeBytes, values);
				} else {
					newbuf = rd.StorageBufferCreate(sizeBytes);
				}

				uniformSizes[buffer] = (int) sizeBytes;
			}
			uniforms[buffer] = newbuf;
			return newbuf;

		} else {
			if (values != null) {
				rd.BufferUpdate(uniforms[buffer], 0u, sizeBytes, values);
			}

			return new Rid();
		}
	}
	
	public Rid SetTexture(StringName texName, uint xSize, uint ySize, Image tex) {
		rd.TextureUpdate(uniforms[texName], 0, tex.GetData());
		return new Rid();
	}
	
	
	public byte[] GetBufferData(StringName buffer) {
		if (uniformTypes[buffer] != RenderingDevice.UniformType.StorageBuffer) {
			return null;
		}
		return rd.BufferGetData(uniforms[buffer]);
	}
	
	public void GetBufferDataAsync(StringName buffer, Callable callback) {
		if (uniformTypes[buffer] != RenderingDevice.UniformType.StorageBuffer) {
			return;
		}
		rd.BufferGetDataAsync(uniforms[buffer], callback);
	}

	public bool HasUniform(StringName buffer) {
		return uniforms.ContainsKey(buffer);
	}

	public void UpdateSync() {
		rd.Submit();
		rd.Sync();
	}

	#endregion
}