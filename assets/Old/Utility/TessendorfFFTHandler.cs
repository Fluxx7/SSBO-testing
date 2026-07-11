// using System;
// using System.Collections.Generic;
// using FluxxiShaderLang;
// using Godot;
// using Godot.Collections;
//
// namespace GodotWaterRendering.assets.Scripts.Utility;
//
// public partial class TessendorfFFTHandler : RefCounted {
// 	public struct TextureCallbacks {
// 		public Action<Rid> spectrumCallback;
// 		public Action<Rid> heightCallback;
// 		public Action<Rid> gradientCallback;
// 		public Action<Rid> foamCallback;
// 		public Action<Rid> normalCallback;
// 	}
// 	private FSLFile tessendorfShader = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/tessendorf_funcs.fsl");
// 	private ComputeGroup tessendorfFuncs;
// 	private List<(FFTHandler, FSLBuffer)> ffts = new();
// 	private uint _N;
// 	
// 	private const uint num_ffts = 2;
// 	private uint num_cascades;
// 	[Export] private float foam_amount = 0.5f;
// 	[Export] private float whitecap = 0.5f;
// 	
// 	private TessendorfFFTHandler() {
// 		_N = 256;
// 	}
// 	
// 	public TessendorfFFTHandler(uint N, uint num_cascades, FSLTexture init_spectrum_texture, FSLBuffer ocean_params, TextureCallbacks callbacks) {
// 		_N = N;
// 		this.num_cascades = num_cascades;
// 		Init(init_spectrum_texture, ocean_params, callbacks);
// 	}
// 	
// 	private void Init(FSLTexture init_spectrum_texture, FSLBuffer ocean_params, TextureCallbacks callbacks) {
// 		tessendorfFuncs = tessendorfShader.GetKernelGroup();
// 		
// 		tessendorfFuncs.AssignResource(init_spectrum_texture, "baseSpectrum");
// 		tessendorfFuncs.AssignResource(ocean_params, "oceanParams");
// 		tessendorfFuncs.TextureSet3D("spectrumTexture", _N, _N, uint.Max(num_cascades, 2));
// 		tessendorfFuncs.TextureBindCallback("spectrumTexture", Callable.From(callbacks.spectrumCallback));
// 		for (var i = 0; i < num_ffts; i++) {
// 			FSLBuffer fft_buffer = tessendorfFuncs.GetBuffer($"fft{i + 1}_buffers");
// 			fft_buffer.SetUnsizedElementCount(_N * _N);
// 			var fft = new FFTHandler(_N, num_cascades, fft_buffer);
// 			ffts.Add((fft, fft_buffer));
// 		}
//
// 		FSLTexture displacement_texture = tessendorfFuncs.GetTexture("heightTexture");
// 		FSLTexture gradient_texture = tessendorfFuncs.GetTexture("gradientTexture");
// 		FSLTexture normal_texture = tessendorfFuncs.GetTexture("normalTexture");
// 		FSLTexture foam_texture = tessendorfFuncs.GetTexture("foamTexture");
// 		
// 		displacement_texture.Set3DTexture(_N, _N, uint.Max(num_cascades, 2));
// 		gradient_texture.Set3DTexture(_N, _N, uint.Max(num_cascades, 2));
// 		normal_texture.Set3DTexture(_N, _N, uint.Max(num_cascades, 2));
// 		foam_texture.Set3DTexture(_N, _N, uint.Max(num_cascades, 2));
// 		
// 		displacement_texture.BindCallback(Callable.From(callbacks.heightCallback));
// 		gradient_texture.BindCallback(Callable.From(callbacks.gradientCallback));
// 		normal_texture.BindCallback(Callable.From(callbacks.normalCallback));
// 		foam_texture.BindCallback(Callable.From(callbacks.foamCallback));
// 	}
//
// 	public void Resize(uint new_size) {
// 		_N = new_size;
// 		foreach (var (fft, buffer) in ffts) {
// 			buffer.SetUnsizedElementCount(new_size * new_size);
// 			fft.Resize(new_size);
// 		}
// 	}
// 	
// 	
// 	public void Run(float delta, float time, float tile_length, float depth) {
//
// 		var finalPlan = ComputePlan.MakeNew(RenderingServer.GetRenderingDevice());
// 		var timePCs = new Godot.Collections.Dictionary<StringName, Variant>{
// 			{"texSize", _N}, 
// 			{"time", time}
// 		};
// 		finalPlan.AddKernel(tessendorfFuncs.GetKernel("updateSpectrum"), _N, _N, num_cascades, timePCs);
// 		var unpackPCs = new Godot.Collections.Dictionary<StringName, Variant> {
// 			{"texSize", _N},
// 			{"foam_grow_rate", foam_amount * delta * 7.5f}, 
// 			{ "foam_decay_rate", delta * float.Max(0.5f, 10f - foam_amount) * 0.15f }, 
// 			{ "whitecap", whitecap }
// 		};
// 		int i = 1;
// 		
// 		foreach (var (fft, _) in ffts) {
// 			var (fftPlan, fftOutput) = fft.Bind();
// 			finalPlan.AddPlan(fftPlan);
// 			unpackPCs.Add($"curr_source{i}", fftOutput);
// 			i++;
// 		}
// 		finalPlan.AddBarrier()
// 			.AddKernel(tessendorfFuncs.GetKernel("ifftUnpack"), _N, _N, num_cascades, unpackPCs);
// 		finalPlan.Dispatch();
// 	}
//
// }