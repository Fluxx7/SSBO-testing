using System;
using System.Collections.Generic;
using Godot;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class ComputeShader(): RefCounted {
	private RenderingDevice prevRd;
	private RDShaderSpirV shaderSpirV;
	private ulong spirvTime;
	private Dictionary<RenderingDevice, Rid> shaderComps = new();
	private Dictionary<RenderingDevice, ulong> shaderCompTimes = new();
	private Dictionary<RenderingDevice, Rid> pipelines = new();
	private StringName shaderPath;
	
	private Dictionary<uint, UniformSet> uniformSets = new();
	
	public ComputeShader(StringName shader_path) : this() {
		shaderPath = shader_path;
	}
	
	public ComputeShader(StringName shader_path, RenderingDevice rd) : this() {
		shaderPath = shader_path;
		prevRd = rd;
	}

	~ComputeShader() {
		Close();
	}

	public void Close() {
		foreach (var set in uniformSets) {
			set.Value.Close();
		}
		uniformSets.Clear();
		foreach (var (rd, pipeline) in pipelines) {
			if (rd.ComputePipelineIsValid(pipeline)) {
				rd.FreeRid(pipeline);
			}
			rd.FreeRid(shaderComps[rd]);
		}
		pipelines.Clear();
		shaderComps.Clear();
	}

	public void SetPath(StringName shader_path) {
		shaderPath = shader_path;
	}

	#region shader


	public Rid GetPipelineRID(RenderingDevice rd) {
		GeneratePipeline(rd);
		return pipelines[rd];
	}

	private void GeneratePipeline(RenderingDevice rd) {
		ulong modifiedTime = FileAccess.GetModifiedTime(shaderPath);
		bool build = false;
		// Update shader compilation and pipeline RID if the shader has been changed since last dispatched
		if (!shaderCompTimes.TryGetValue(rd, out ulong value)) {
			build = true;
		} else if (value != modifiedTime) {
			build = true;
		}

		if (build) {
			shaderCompTimes[rd] = modifiedTime;
			if (shaderComps.TryGetValue(rd, out Rid shaderComp)) {
				rd.FreeRid(shaderComp);
				if (rd.ComputePipelineIsValid(pipelines[rd])) {
					rd.FreeRid(pipelines[rd]);
				}
			}
			if (spirvTime != modifiedTime) {
				shaderSpirV = GD.Load<RDShaderFile>(shaderPath).GetSpirV();
			}

			shaderComps[rd] = rd.ShaderCreateFromSpirV(shaderSpirV);
			pipelines[rd] = rd.ComputePipelineCreate(shaderComps[rd]);
		}
	}


	public Dictionary<uint, Rid> GetUniformSetRIDs(RenderingDevice rd) {
		Dictionary<uint, Rid> uniformSetIDs = new();
		foreach (var (index, set) in uniformSets) {
			uniformSetIDs[index] = set.GetRID(rd, shaderComps[rd], index);
		}

		return uniformSetIDs;
	}

	public void Dispatch(RenderingDevice rd, uint x_threads, uint y_threads, uint z_threads, byte[] push_constants = null) {
		prevRd = rd;
		GeneratePipeline(rd);
		Dictionary<uint, Rid> uniformSetIDs = GetUniformSetRIDs(rd);
		
		var computeList = rd.ComputeListBegin();
		rd.ComputeListBindComputePipeline(computeList, pipelines[rd]);
		if (push_constants != null) {
			rd.ComputeListSetPushConstant(computeList, push_constants, (uint) push_constants.Length);
		}
		foreach (var (index, set) in uniformSetIDs) {
			rd.ComputeListBindUniformSet(computeList, set, index);
		}
		rd.ComputeListDispatch(computeList, x_threads, y_threads, z_threads);
		rd.ComputeListEnd();
	}

	public void Dispatch(RenderingDevice rd, uint x_threads, uint y_threads, uint z_threads, ByteBuffer push_constants) {
		Dispatch(rd, x_threads, y_threads, z_threads, push_constants.Generate());
	}
	
	public void Dispatch(uint x_threads, uint y_threads, uint z_threads, byte[] push_constants = null) {
		prevRd ??= RenderingServer.GetRenderingDevice();
		Dispatch(prevRd, x_threads, y_threads, z_threads, push_constants);
	}
	
	
	
	public void Dispatch(uint x_threads, uint y_threads, uint z_threads, ByteBuffer push_constants) {
		prevRd ??= RenderingServer.GetRenderingDevice();
		Dispatch(prevRd, x_threads, y_threads, z_threads, push_constants);
	}
	
#endregion
	
#region uniforms

	public void CreateBuffer(StringName buffer, RenderingDevice.UniformType type, uint size_bytes, uint set, uint binding, byte[] data = null) {
		CreateBuffer(buffer, type, size_bytes, data);
		AssignUniform(buffer, set, binding);
	}

	public void CreateTexture(StringName texture, uint x_size, uint y_size, uint set, uint binding) {
		CreateTexture(texture, x_size, y_size);
		AssignUniform(texture, set, binding);
	}

	public void AssignUniform(StringName uniform, uint set, uint binding) {
		if (!uniformSets.TryGetValue(set, out UniformSet value)) {
            value = new UniformSet();
            uniformSets[set] = value;
		}

        value.BindUniform(uniform, binding);
	}
	
	
	
	
	public byte[] GetBufferData(StringName buffer) {
		return GetBufferData(prevRd, buffer);
	}
	
	public void GetBufferDataAsync(StringName buffer, Callable callback) {
		GetBufferDataAsync(prevRd, buffer, callback);
	}

	#endregion
	
	#region static 
	
	public static byte[] MakePushConstants(byte[] values = null) {
		
		return [];
	}
	public static void SetBuffer(StringName buffer, byte[] values = null) {
		ShaderResourceStorage.SetBuffer(buffer, values);
	}
	
	public static void SetBufferSize(StringName buffer, uint size) {
		ShaderResourceStorage.SetBufferSize(buffer, size);
	}

	
	public static void SetTexture(StringName tex_name, uint x_size, uint y_size, Image tex) {
		ShaderResourceStorage.SetTexture(tex_name, x_size, y_size, tex);
	}
	
	public static void SetTextureSize(StringName tex_name, uint x_size, uint y_size) {
		ShaderResourceStorage.SetTextureSize(tex_name, x_size, y_size);
	}
	
	public static void CreateBuffer(StringName buffer, RenderingDevice.UniformType type, uint size_bytes, byte[] data = null) {
		ShaderResourceStorage.CreateBuffer(buffer, type, size_bytes, data);
	}

	public static void CreateTexture(StringName texture, uint x_size, uint y_size) {
		ShaderResourceStorage.CreateTexture(texture, x_size, y_size);
	}
	
	public static void CreateTexture(StringName texture, uint x_size, uint y_size, TextureResource.TextureType texture_type) {
		ShaderResourceStorage.CreateTexture(texture, x_size, y_size, texture_type);
	}
	
	public static byte[] GetBufferData(RenderingDevice rd, StringName buffer) {
		return ShaderResourceStorage.GetBufferData(buffer, rd);
	}
	
	public static void GetBufferDataAsync(RenderingDevice rd, StringName buffer, Callable callback) {
		ShaderResourceStorage.GetBufferDataAsync(buffer, rd, callback);
	}
	
	public static void BindTextureParameter(StringName texture, Callable callback) {
		ShaderResourceStorage.BindTextureParameter(texture, callback);
	}
	
	#endregion
}