using System;
using System.Collections.Generic;
using Godot;
using GraphicsTesting.Libraries.ComputeShaderHandling;

namespace GraphicsTesting.assets.Scripts.Utility;

public partial class TessendorfFFTHandler: RefCounted {
	private ComputeShader twiddleGen = new("res://assets/Shaders/Compute/GLSL/FFT/prep_twiddle.glsl");
	private ComputeShader tessendorfUnpack = new("res://assets/Shaders/Compute/GLSL/FFT/tessendorf_ifft_unpack.glsl");
	private ComputeShader updateSpectrum = new("res://assets/Shaders/Compute/GLSL/FFT/update_spectrum.glsl");
	private List<(FFTHandler, uint)> ffts = new();
	private uint _N;
	private ByteBuffer nBuffer;
	private const uint vec4_size = 16;
	
	private const uint num_ffts = 2;
	[Export] private float foam_amount = 0.5f;
	[Export] private float whitecap = 0.5f;
	
	private TessendorfFFTHandler() {
		_N = 256;
		Init("", "", "", "", "");
	}

	public TessendorfFFTHandler(uint N, StringName init_spectrum_texture, StringName spectrum_texture, StringName displacement_texture, StringName gradient_texture, StringName foam_texture) {
		_N = N;
		Init(init_spectrum_texture, spectrum_texture, displacement_texture, gradient_texture, foam_texture);
	}
	
	public TessendorfFFTHandler(uint N, uint init_spectrum_texture, uint spectrum_texture, uint displacement_texture, uint gradient_texture, uint foam_texture, uint normal_texture) {
		_N = N;
		Init(init_spectrum_texture, spectrum_texture, displacement_texture, gradient_texture, foam_texture, normal_texture);
	}

	~TessendorfFFTHandler() {
		twiddleGen.Close();
		tessendorfUnpack.Close();
	}

	private void Init(StringName init_spectrum_texture, StringName spectrum_texture, StringName displacement_texture, StringName gradient_texture, StringName foam_texture) {
		nBuffer = new ByteBuffer(_N);
		updateSpectrum.AssignUniform(init_spectrum_texture, 0, 0);
		updateSpectrum.AssignUniform(spectrum_texture, 0, 1);
		for (var i = 0; i < num_ffts; i++) {
			uint id = updateSpectrum.CreateInternalBuffer(RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec4_size, 1, (uint) i);
			tessendorfUnpack.AssignUniform(id, 0, (uint) i);
			var fft = new FFTHandler(_N, id);
			ffts.Add((fft, id));
		}
		
		tessendorfUnpack.AssignUniform(displacement_texture, 1, 0);
		tessendorfUnpack.AssignUniform(gradient_texture, 1, 1);
		tessendorfUnpack.AssignUniform(foam_texture, 1, 2);
		
	}
	
	private void Init(uint init_spectrum_texture, uint spectrum_texture, uint displacement_texture, uint gradient_texture, uint foam_texture, uint normal_texture) {
		nBuffer = new ByteBuffer(_N);
		updateSpectrum.AssignUniform(init_spectrum_texture, 0, 0);
		updateSpectrum.AssignUniform(spectrum_texture, 0, 1);
		for (var i = 0; i < num_ffts; i++) {
			uint id = updateSpectrum.CreateInternalBuffer(RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec4_size, 1, (uint) i);
			tessendorfUnpack.AssignUniform(id, 0, (uint) i);
			var fft = new FFTHandler(_N, id);
			ffts.Add((fft, id));
		}
		
		tessendorfUnpack.AssignUniform(displacement_texture, 1, 0);
		tessendorfUnpack.AssignUniform(gradient_texture, 1, 1);
		tessendorfUnpack.AssignUniform(foam_texture, 1, 2);
		tessendorfUnpack.AssignUniform(normal_texture, 1, 3);
		
	}

	public void Resize(uint new_size) {
		_N = new_size;
		nBuffer = new ByteBuffer(_N);
		foreach (var (fft, buffer_id) in ffts) {
			ComputeShader.SetInternalBufferSize(buffer_id, new_size * new_size * 2 * vec4_size);
			fft.Resize(new_size);
		}
	}
	
	
	public void Run(float delta, float time, float tile_length, float depth) {

		var finalPlan = new ComputePlan();
		ByteBuffer timeBuffer = new ByteBuffer().Add(_N).Add([time, tile_length, depth]);
		finalPlan.AddShader(updateSpectrum, _N / 16, _N / 16, 1, 
			timeBuffer);
		ByteBuffer unpackBuffer = new ByteBuffer(_N).Add([foam_amount * delta * 7.5f, delta * float.Max(0.5f, 10f - foam_amount) * 0.15f, whitecap]);
		foreach (var (fft, _) in ffts) {
			var (fftPlan, fftOutput) = fft.Bind();
			finalPlan.AddPlan(fftPlan);
			unpackBuffer.Add(fftOutput);
		}
		finalPlan.AddBarrier()
			.AddShader(tessendorfUnpack, _N / 16, _N / 16, 1,
				unpackBuffer);
		finalPlan.Dispatch();
	}

}