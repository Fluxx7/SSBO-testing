using System;
using Godot;
using GraphicsTesting.Libraries.ComputeShaderHandling;

namespace GraphicsTesting.assets.Scripts.Utility;

public partial class TessendorfFFTHandler: RefCounted {
	private ComputeShader twiddleGen = new("res://assets/Shaders/Compute/GLSL/FFT/prep_twiddle.glsl");
	private ComputeShader updateSpectrum = new("res://assets/Shaders/Compute/GLSL/FFT/update_spectrum.glsl");
	private ComputeShader naiveIfftStage = new("res://assets/Shaders/Compute/GLSL/FFT/naive_ifft_2d_stage.glsl");
	private ComputeShader ifftTranspose = new("res://assets/Shaders/Compute/GLSL/FFT/ifft_2d_transpose.glsl");
	private ComputeShader tessendorfUnpack = new("res://assets/Shaders/Compute/GLSL/FFT/tessendorf_ifft_unpack.glsl");
	private ComputePlan ifftComputePlan;
	private uint _N;
	private ByteBuffer nBuffer;
	private uint outputBuffer = 0;
	private const uint vec2_size = 8;
	private const uint vec4_size = 16;
	private bool twiddlesGenned = false;

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
		updateSpectrum.Close();
		naiveIfftStage.Close();
		ifftTranspose.Close();
		tessendorfUnpack.Close();
	}

	private void Init(StringName init_spectrum_texture, StringName spectrum_texture, StringName displacement_texture, StringName gradient_texture) {
		nBuffer = new ByteBuffer(_N);
		twiddleGen.CreateBuffer("twiddleFactors", RenderingDevice.UniformType.StorageBuffer, _N / 2 * vec2_size, 0, 0);
		
		updateSpectrum.AssignUniform(init_spectrum_texture, 0, 0);
		updateSpectrum.AssignUniform(spectrum_texture, 0, 1);
		updateSpectrum.CreateBuffer("fft_data", RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec2_size, 1, 0);
		updateSpectrum.CreateBuffer("fft_grad_data", RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec4_size, 1, 1);
		updateSpectrum.CreateBuffer("fft_disp_data", RenderingDevice.UniformType.StorageBuffer, _N * _N * 2 * vec4_size, 1, 2);
		
		naiveIfftStage.AssignUniform("twiddleFactors", 0, 0);
		naiveIfftStage.AssignUniform("fft_data",1, 0);
		naiveIfftStage.AssignUniform("fft_grad_data",1, 1);
		naiveIfftStage.AssignUniform("fft_disp_data",1, 2);
		
		ifftTranspose.AssignUniform("fft_data", 0, 0);
		ifftTranspose.AssignUniform("fft_grad_data",0, 1);
		ifftTranspose.AssignUniform("fft_disp_data",0, 2);
		
		tessendorfUnpack.AssignUniform("fft_data", 0, 0);
		tessendorfUnpack.AssignUniform("fft_grad_data",0, 1);
		tessendorfUnpack.AssignUniform("fft_disp_data",0, 2);
		tessendorfUnpack.AssignUniform(displacement_texture, 1, 0);
		tessendorfUnpack.AssignUniform(gradient_texture, 1, 1);
		
		BuildIFFTComputePlan();
		
	}

	public void Resize(uint new_size) {
		_N = new_size;
		nBuffer = new ByteBuffer(_N);
		ComputeShader.SetBufferSize("twiddleFactors", new_size / 2 * vec2_size );
		ComputeShader.SetBufferSize("fft_data", new_size * new_size * 2 * vec2_size);
		ComputeShader.SetBufferSize("fft_grad_data", new_size * new_size * 2 * vec4_size);
		twiddlesGenned = false;
		BuildIFFTComputePlan();
	}

	private void BuildIFFTComputePlan() {
		ifftComputePlan = new ComputePlan();
		var logN = (uint) Math.Log2(_N);
		var currSource = 0u;
		var currDest = 1u;
		for (uint stage = 0; stage < logN; stage++) {
			ifftComputePlan.AddBarrier();
			
			var pushConstants = new ByteBuffer();
			pushConstants.Add(_N).Add(stage).Add(currSource).Add(currDest);
			
			ifftComputePlan.AddShader(naiveIfftStage, (_N / 2) / 16, _N / 16, 1, pushConstants);
		
			(currSource, currDest) = (currDest, currSource);
		}
		
		ifftComputePlan.AddBarrier();
		ifftComputePlan.AddShader(ifftTranspose, _N / 16, _N / 16, 1, 
			new ByteBuffer().Add(_N).Add(currSource).Add(currDest));
		
		(currSource, currDest) = (currDest, currSource);
		
		for (uint stage = 0; stage < logN; stage++) {
			ifftComputePlan.AddBarrier();
			
			var pushConstants = new ByteBuffer();
			pushConstants.Add(_N).Add(stage).Add(currSource).Add(currDest);
			
			ifftComputePlan.AddShader(naiveIfftStage, (_N / 2) / 16, _N / 16, 1, pushConstants);
		
			(currSource, currDest) = (currDest, currSource);
		}
		
		outputBuffer = currSource;
	}
	

	public void Run(float time, float tile_length, float depth) {
		if (!twiddlesGenned) {
			twiddleGen.Dispatch((_N / 2) / 16, 1, 1, nBuffer);
			twiddlesGenned = true;
		}

		var finalPlan = new ComputePlan();
		ByteBuffer timeBuffer = new ByteBuffer().Add(_N).Add([time, tile_length, depth]);
		finalPlan.AddShader(updateSpectrum, _N / 16, _N / 16, 1, 
			timeBuffer);
		finalPlan.AddPlan(ifftComputePlan);
		finalPlan.AddBarrier()
			.AddShader(tessendorfUnpack, _N / 16, _N / 16, 1,
			timeBuffer.Add(outputBuffer));
		finalPlan.Dispatch();
	}

}