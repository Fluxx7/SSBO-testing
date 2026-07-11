// using System;
// using FluxxiShaderLang;
// using Godot;
// using Godot.Collections;
//
// namespace GodotWaterRendering.assets.Scripts.Utility;
//
// public partial class FFTHandler : RefCounted {
// 	private FSLFile fftShader = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/fft.fsl");
// 	private ComputeGroup fftKernels;
// 	private ComputePlan ifftComputePlan;
// 	private uint _N = 256;
// 	private uint outputBuffer;
// 	private uint num_cascades;
// 	
// 	private bool twiddlesGenned = false;
//
// 	private FFTHandler() {
// 		_N = 256;
// 	}
// 	
// 	public FFTHandler(uint N, uint num_cascades, FSLBuffer fft_data_buffer) {
// 		_N = N;
// 		this.num_cascades = num_cascades;
// 		Init(fft_data_buffer);
// 	}
// 	
// 	private void Init(FSLBuffer fft_data_buffer) {
// 		fftKernels = fftShader.GetKernelGroup();
// 		
// 		fftKernels.BufferSetUnsizedElementCount("twiddleFactors", _N / 2);
// 		fftKernels.AssignResource(fft_data_buffer,"fft_buffers");
// 		
// 		BuildIFFTComputePlan();
// 	}
//
// 	public void Resize(uint new_size) {
// 		_N = new_size;
// 		fftKernels.BufferSetUnsizedElementCount("twiddleFactors", _N / 2);
// 		twiddlesGenned = false;
// 		BuildIFFTComputePlan();
// 	}
//
// 	private void BuildIFFTComputePlan() {
// 		ifftComputePlan = ComputePlan.MakeNew(RenderingServer.GetRenderingDevice());
// 		var logN = (uint) Math.Log2(_N);
// 		var currSource = 0u;
// 		var currDest = 1u;
// 		for (uint stage = 0; stage < logN; stage++) {
// 			ifftComputePlan.AddBarrier();
// 			
// 			var pushConstants = new Dictionary<StringName, Variant> {
// 				{"N", _N},
// 				{"stage", stage},
// 				{"curr_source", currSource},
// 				{"curr_dest", currDest}
// 			};
// 			
// 			ifftComputePlan.AddKernel(fftKernels.GetKernel("ifftStage"), _N / 2, _N, num_cascades, pushConstants);
// 		
// 			(currSource, currDest) = (currDest, currSource);
// 		}
// 		
// 		ifftComputePlan.AddBarrier();
// 		ifftComputePlan.AddKernel(fftKernels.GetKernel("ifftTranspose"), _N, _N, num_cascades, 
// 			new Dictionary<StringName, Variant> {
// 				{ "N", _N }, 
// 				{"curr_source", currSource },
// 				{ "curr_dest", currDest }});
// 		
// 		(currSource, currDest) = (currDest, currSource);
// 		
// 		for (uint stage = 0; stage < logN; stage++) {
// 			ifftComputePlan.AddBarrier();
// 			var pushConstants = new Dictionary<StringName, Variant> {
// 				{"N", _N},
// 				{"stage", stage},
// 				{"curr_source", currSource},
// 				{"curr_dest", currDest}
// 			};
// 			
// 			ifftComputePlan.AddKernel(fftKernels.GetKernel("ifftStage"), _N / 2, _N, num_cascades, pushConstants);
// 		
// 			(currSource, currDest) = (currDest, currSource);
// 		}
// 		
// 		ifftComputePlan.AddBarrier();
// 		ifftComputePlan.AddKernel(fftKernels.GetKernel("ifftTranspose"), _N, _N, num_cascades, 
// 			new Dictionary<StringName, Variant> {
// 				{ "N", _N }, 
// 				{"curr_source", currSource },
// 				{ "curr_dest", currDest }});
// 		
// 		(currSource, currDest) = (currDest, currSource);
// 		
// 		outputBuffer = currSource;
// 	}
// 	
//
// 	public (ComputePlan, uint) Bind() {
// 		if (!twiddlesGenned) {
// 			fftKernels.Dispatch("twiddleGen", _N / 2, 1, 1, new Dictionary<StringName, Variant>{{"N", _N}});
// 			twiddlesGenned = true;
// 		}
//
// 		return (ifftComputePlan, outputBuffer);
// 	}
//
// }