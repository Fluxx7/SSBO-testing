using System;
using System.Collections.Generic;
using Godot;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class ComputePlan: RefCounted {

	private enum ComputePlanItem {
		Shader,
		Barrier,
		Plan
	}
	
	private struct ShaderInvocation {
		public ComputeShader shader;
		public uint x_threads;
		public uint y_threads;
		public uint z_threads;
		public byte[] push_constants;
	}

	private RenderingDevice prevRd;
	private List<ShaderInvocation> invocations = [];
	private List<ComputePlan> plans = [];
	private List<(ComputePlanItem, uint)> computeItems = [];
	private List<(ComputePlanItem, uint)> tempItems = [];
	private List<ShaderInvocation> tempInvocations = [];
	
	#region shader


	public ComputePlan AddShader(ComputeShader shader, uint x_threads, uint y_threads, uint z_threads, byte[] push_constants = null) {
		ShaderInvocation invocation = new ShaderInvocation();
		invocation.shader = shader;
		invocation.x_threads = x_threads;
		invocation.y_threads = y_threads;
		invocation.z_threads = z_threads;
		invocation.push_constants = push_constants;
		computeItems.Add((ComputePlanItem.Shader, (uint) invocations.Count));
		invocations.Add(invocation);
		return this;
	}
	
	public ComputePlan AddShader(ComputeShader shader, uint x_threads, uint y_threads, uint z_threads, ByteBuffer push_constants) {
		return AddShader(shader, x_threads, y_threads, z_threads, push_constants.Generate(4));
	}

	public ComputePlan AddBarrier() {
		computeItems.Add((ComputePlanItem.Barrier, 0));
		return this;
	}
	
	public ComputePlan AddTempShader(ComputeShader shader, uint x_threads, uint y_threads, uint z_threads, byte[] push_constants = null) {
		ShaderInvocation invocation = new ShaderInvocation();
		invocation.shader = shader;
		invocation.x_threads = x_threads;
		invocation.y_threads = y_threads;
		invocation.z_threads = z_threads;
		invocation.push_constants = push_constants;
		tempItems.Add((ComputePlanItem.Shader, (uint) tempInvocations.Count));
		tempInvocations.Add(invocation);
		return this;
	}
	
	public ComputePlan AddTempShader(ComputeShader shader, uint x_threads, uint y_threads, uint z_threads, ByteBuffer push_constants) {
		return AddTempShader(shader, x_threads, y_threads, z_threads, push_constants.Generate(4));
	}
	
	public ComputePlan AddTempBarrier() {
		tempItems.Add((ComputePlanItem.Barrier, 0));
		return this;
	}

	public void Dispatch(RenderingDevice rd) {
		prevRd = rd;
		
		var computeList = rd.ComputeListBegin();
		Bind(rd, computeList);
		rd.ComputeListEnd();
	}

	
	public void Dispatch() {
		prevRd ??= RenderingServer.GetRenderingDevice();
		Dispatch(prevRd);
	}

	private void Bind(RenderingDevice rd, long computeList) {
		foreach (var (type, group_index) in computeItems) {
			if (type == ComputePlanItem.Barrier) {
				rd.ComputeListAddBarrier(computeList);
				continue;
			}
			if (type == ComputePlanItem.Plan) {
				plans[(int)group_index].Bind(rd, computeList);
				continue;
			}
			ShaderInvocation invocation = invocations[(int)group_index];
			rd.ComputeListBindComputePipeline(computeList, invocation.shader.GetPipelineRID(rd));
			if (invocation.push_constants != null) {
				rd.ComputeListSetPushConstant(computeList, invocation.push_constants, (uint) invocation.push_constants.Length);
			}
			foreach (var (index, set) in invocation.shader.GetUniformSetRIDs(rd)) {
				rd.ComputeListBindUniformSet(computeList, set, index);
			}
			rd.ComputeListDispatch(computeList, invocation.x_threads, invocation.y_threads, invocation.z_threads);
		}
		
		foreach (var (type, group_index) in tempItems) {
			if (type == ComputePlanItem.Barrier) {
				rd.ComputeListAddBarrier(computeList);
				continue;
			}
			ShaderInvocation invocation = tempInvocations[(int)group_index];
			rd.ComputeListBindComputePipeline(computeList, invocation.shader.GetPipelineRID(rd));
			if (invocation.push_constants != null) {
				rd.ComputeListSetPushConstant(computeList, invocation.push_constants, (uint) invocation.push_constants.Length);
			}
			foreach (var (index, set) in invocation.shader.GetUniformSetRIDs(rd)) {
				rd.ComputeListBindUniformSet(computeList, set, index);
			}
			rd.ComputeListDispatch(computeList, invocation.x_threads, invocation.y_threads, invocation.z_threads);
		}
		tempItems.Clear();
		tempInvocations.Clear();
	}
	
	public void AddPlan(ComputePlan subplan) {
		computeItems.Add((ComputePlanItem.Plan, (uint) plans.Count));
		plans.Add(subplan);
	}
	
#endregion

}