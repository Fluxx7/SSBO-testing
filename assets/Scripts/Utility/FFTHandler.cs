using System;
using System.Collections.Generic;
using Godot;
using GraphicsTesting.Libraries.ComputeShaderHandling;

namespace GraphicsTesting.assets.Scripts.Utility;

public partial class FFTHandler: RefCounted {
	private ComputeShader twiddleGen = new("res://assets/Shaders/Compute/GLSL/FFT/prep_twiddle.glsl");
	private ComputeShader naiveIfftStage = new("res://assets/Shaders/Compute/GLSL/FFT/naive_ifft_2d_stage.glsl");
	private ComputeShader ifftTranspose = new("res://assets/Shaders/Compute/GLSL/FFT/ifft_2d_transpose.glsl");
	private ComputePlan ifftComputePlan;
	private uint _N;
	private ByteBuffer nBuffer;
	private uint outputBuffer = 0;
	private const uint vec2_size = 8;
	private const uint vec4_size = 16;
	
	private bool twiddlesGenned = false;
	private Dictionary<StringName, uint> uniforms = new();

	private FFTHandler() {
		_N = 256;
		Init("");
	}

	public FFTHandler(uint N, StringName fft_data_buffer) {
		_N = N;
		Init(fft_data_buffer);
	}

	~FFTHandler() {
		twiddleGen.Close();
		naiveIfftStage.Close();
		ifftTranspose.Close();
	}

	private void Init(StringName fft_data_buffer) {
		nBuffer = new ByteBuffer(_N);
		uint twiddles_id = twiddleGen.CreateInternalBuffer(RenderingDevice.UniformType.StorageBuffer, _N / 2 * vec2_size, 0, 0);
		
		naiveIfftStage.AssignUniform(twiddles_id, 0, 0);
		naiveIfftStage.AssignUniform(fft_data_buffer,1, 0);
		
		ifftTranspose.AssignUniform(fft_data_buffer, 0, 0);
		uniforms.Add("twiddles", twiddles_id);
		BuildIFFTComputePlan();
		
	}

	public void Resize(uint new_size) {
		_N = new_size;
		nBuffer = new ByteBuffer(_N);
		ComputeShader.SetInternalBufferSize(uniforms["twiddles"], new_size / 2 * vec2_size );
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
	

	public (ComputePlan, uint) Bind() {
		if (!twiddlesGenned) {
			twiddleGen.Dispatch((_N / 2) / 16, 1, 1, nBuffer);
			twiddlesGenned = true;
		}

		return (ifftComputePlan, outputBuffer);
	}

}