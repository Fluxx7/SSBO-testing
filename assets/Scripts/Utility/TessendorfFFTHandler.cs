using System;
using System.Collections.Generic;
using Godot;
using GraphicsTesting.Libraries.ComputeShaderHandling;

namespace GraphicsTesting.assets.Scripts.Utility;

public partial class TessendorfFFTHandler: RefCounted {
	private ComputeShader twiddleGen = new("res://assets/Shaders/Compute/GLSL/FFT/prep_twiddle.glsl");
	private ComputeShader tessendorfUnpack = new("res://assets/Shaders/Compute/GLSL/FFT/tessendorf_ifft_unpack.glsl");
	private ComputeShader updateSpectrum = new("res://assets/Shaders/Compute/GLSL/FFT/update_spectrum.glsl");
	private FFTHandler heightFFT;
	private FFTHandler displacementFFT;
	private FFTHandler gradientsFFT;
	private uint _N;
	private ByteBuffer nBuffer;
	private const uint vec2_size = 8;
	private const uint vec4_size = 16;

	private TessendorfFFTHandler() {
		_N = 256;
		Init("", "", "", "");
	}

	public TessendorfFFTHandler(uint N, StringName init_spectrum_texture, StringName spectrum_texture, StringName displacement_texture, StringName gradient_texture) {
		_N = N;
		Init(init_spectrum_texture, spectrum_texture, displacement_texture, gradient_texture);
	}

	~TessendorfFFTHandler() {
		twiddleGen.Close();
		tessendorfUnpack.Close();
	}

	private void Init(StringName init_spectrum_texture, StringName spectrum_texture, StringName displacement_texture, StringName gradient_texture) {
		nBuffer = new ByteBuffer(_N);
		updateSpectrum.AssignUniform(init_spectrum_texture, 0, 0);
		updateSpectrum.AssignUniform(spectrum_texture, 0, 1);
		updateSpectrum.CreateBuffer("fft_data", RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec4_size, 1, 0);
		updateSpectrum.CreateBuffer("fft_grad_data", RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec4_size, 1, 1);
		updateSpectrum.CreateBuffer("fft_disp_data", RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec4_size, 1, 2);
		heightFFT = new FFTHandler(_N, "fft_data");
		displacementFFT = new FFTHandler(_N, "fft_disp_data");
		gradientsFFT = new FFTHandler(_N, "fft_grad_data");
		
		tessendorfUnpack.AssignUniform("fft_data", 0, 0);
		tessendorfUnpack.AssignUniform("fft_grad_data",0, 1);
		tessendorfUnpack.AssignUniform("fft_disp_data",0, 2);
		tessendorfUnpack.AssignUniform(displacement_texture, 1, 0);
		tessendorfUnpack.AssignUniform(gradient_texture, 1, 1);
		
	}

	public void Resize(uint new_size) {
		_N = new_size;
		nBuffer = new ByteBuffer(_N);
		ComputeShader.SetBufferSize("fft_data", new_size * new_size * 2 * vec4_size);
		ComputeShader.SetBufferSize("fft_grad_data", new_size * new_size * 2 * vec4_size);
		ComputeShader.SetBufferSize("fft_disp_data", new_size * new_size * 2 * vec4_size);
	}
	

	public void Run(float time, float tile_length, float depth) {

		var finalPlan = new ComputePlan();
		ByteBuffer timeBuffer = new ByteBuffer().Add(_N).Add([time, tile_length, depth]);
		finalPlan.AddShader(updateSpectrum, _N / 16, _N / 16, 1, 
			timeBuffer);
		var (heightPlan, heightOutput) = heightFFT.Bind();
		var (displacementPlan, displacementOutput) = displacementFFT.Bind();
		var (gradientPlan, gradientOutput) = gradientsFFT.Bind();
		finalPlan.AddPlan(heightPlan);
		finalPlan.AddPlan(displacementPlan);
		finalPlan.AddPlan(gradientPlan);
		finalPlan.AddBarrier()
			.AddShader(tessendorfUnpack, _N / 16, _N / 16, 1,
			timeBuffer.Add(heightOutput).Add(displacementOutput).Add(gradientOutput).Generate());
		finalPlan.Dispatch();
	}

}