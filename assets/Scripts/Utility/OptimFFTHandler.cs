using System;
using System.Collections.Generic;
using FluxxiShaderLang;
using Godot;
using Godot.Collections;

namespace GodotWaterRendering.assets.Scripts.Utility;

public partial class OptimFFTHandler : RefCounted {
	public struct TextureCallbacks {
		public Action<Rid> spectrumCallback;
		public Action<Rid> heightCallback;
		public Action<Rid> gradientCallback;
		public Action<Rid> foamCallback;
	}
	private FSLFile tessendorfShader = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/tess_funcs_optim.fsl");
	private FSLFile fftShader = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/fft_optim.fsl");
	private ComputeGroup tessendorfFuncs;
	private ComputeKernel twiddleGen;
	private List<ComputeGroup> ffts = new();
	private uint _N;
	
	private const uint num_ffts = 4;
	private uint num_cascades;
	[Export] private float foam_amount = 0.5f;
	[Export] private float whitecap = 0.5f;
	private bool twiddlesGenned = false;
	
	private OptimFFTHandler() {
		_N = 256;
	}
	
	public OptimFFTHandler(uint N, uint num_cascades, FSLTexture init_spectrum_texture, FSLBuffer ocean_params, TextureCallbacks callbacks) {
		_N = N;
		this.num_cascades = num_cascades;
		Init(init_spectrum_texture, ocean_params, callbacks);
	}

	public void UpdateCascadeCount(uint new_count) {
		num_cascades = new_count;
		uint safeCascades = uint.Max(new_count, 2);
		tessendorfFuncs.TextureSet3D("spectrumTexture", _N, _N, safeCascades);
		foreach (var fft in ffts) {
			fft.BufferSetUnsizedElementCount("fft_buffers", _N * _N * num_cascades);
		}
		tessendorfFuncs.TextureSet3D("heightTexture", _N, _N, safeCascades);
		tessendorfFuncs.TextureSet3D("gradientTexture", _N, _N, safeCascades);
		tessendorfFuncs.TextureSet3D("foamTexture", _N, _N, safeCascades);
	}

	private void Init(FSLTexture init_spectrum_texture, FSLBuffer ocean_params, TextureCallbacks callbacks) {
		tessendorfFuncs = tessendorfShader.GetKernelGroup();
		
		tessendorfFuncs.AssignResource(init_spectrum_texture, "baseSpectrum");
		tessendorfFuncs.AssignResource(ocean_params, "oceanParams");
		tessendorfFuncs.TextureSet3D("spectrumTexture", _N, _N, uint.Max(num_cascades, 2));
		tessendorfFuncs.TextureBindCallback("spectrumTexture", Callable.From(callbacks.spectrumCallback));

		twiddleGen = fftShader.GetKernel("twiddleGen");
		FSLBuffer twiddleBuffer = twiddleGen.GetBuffer("twiddleFactors");
		twiddleBuffer.SetUnsizedElementCount(_N / 2);
		for (var i = 0; i < num_ffts; i++) {
			ComputeGroup fft = fftShader.GetKernelGroup();
			FSLBuffer fft_buffer = tessendorfFuncs.GetBuffer($"fft{i + 1}_buffers");
			fft_buffer.SetUnsizedElementCount(_N * _N * num_cascades);
			fft.AssignResource(fft_buffer, "fft_buffers");
			fft.AssignResource(twiddleBuffer, "twiddleFactors");
			ffts.Add(fft);
		}

		FSLTexture displacement_texture = tessendorfFuncs.GetTexture("heightTexture");
		FSLTexture gradient_texture = tessendorfFuncs.GetTexture("gradientTexture");
		FSLTexture foam_texture = tessendorfFuncs.GetTexture("foamTexture");
		
		displacement_texture.Set3DTexture(_N, _N, uint.Max(num_cascades, 2));
		gradient_texture.Set3DTexture(_N, _N, uint.Max(num_cascades, 2));
		foam_texture.Set3DTexture(_N, _N, uint.Max(num_cascades, 2));
		
		displacement_texture.BindCallback(Callable.From(callbacks.heightCallback));
		gradient_texture.BindCallback(Callable.From(callbacks.gradientCallback));
		foam_texture.BindCallback(Callable.From(callbacks.foamCallback));
	}

	public void Resize(uint new_size) {
		_N = new_size;
		foreach (var fft in ffts) {
			fft.BufferSetUnsizedElementCount("fft_buffers", _N * _N * num_cascades);
			fft.BufferSetUnsizedElementCount("twiddleFactors", _N / 2);
		}
	}
	
	
	public void Run(float delta, float time, float tile_length, float depth) {
		if (!twiddlesGenned) {
			// fft.Dispatch("twiddleGen", _N / 2, 1, 1, new Dictionary<StringName, Variant>{{"N", _N}});
			twiddleGen.Dispatch(_N/2, 1, 1, new Godot.Collections.Dictionary<StringName, Variant>{{"N", _N}});
			twiddlesGenned = true;
		}
		var fftPlan = ComputePlan.MakeNew(RenderingServer.GetRenderingDevice());
		var timePCs = new Godot.Collections.Dictionary<StringName, Variant>{
			{"texSize", _N}, 
			{"time", time}
		};
		fftPlan.AddKernel(tessendorfFuncs.GetKernel("updateSpectrum"), _N, _N, num_cascades, timePCs);
		var unpackPCs = new Godot.Collections.Dictionary<StringName, Variant> {
			{"texSize", _N},
			{"foam_grow_rate", foam_amount * delta * 7.5f}, 
			{ "foam_decay_rate", delta * float.Max(0.5f, 10f - foam_amount) * 0.15f }, 
			{ "whitecap", whitecap }
		};
		
		foreach (ComputeGroup fft in ffts) {
			fftPlan.AddKernel(fft.GetKernel("ifftRow"), _N / 2, _N, num_cascades);
		}
		
		fftPlan.AddBarrier();
		foreach (ComputeGroup fft in ffts) {
			fftPlan.AddKernel(fft.GetKernel("ifftColumn"), _N / 2, _N, num_cascades);
		}
		fftPlan.AddBarrier()
			.AddKernel(tessendorfFuncs.GetKernel("ifftUnpack"), _N, _N, num_cascades, unpackPCs);
		fftPlan.Dispatch();
	}

}