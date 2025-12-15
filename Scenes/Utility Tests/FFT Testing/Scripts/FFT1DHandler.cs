using Godot;
using System;
using System.Text;
using GraphicsTesting.assets.Scripts.Utility;
using GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class FFT1DHandler : GodotObject {
	private ComputeShader fft1d = new("res://Scenes/Utility Tests/FFT Testing/Compute/fft_1d.glsl");
	private ComputeShader ifft1d = new("res://Scenes/Utility Tests/FFT Testing/Compute/ifft_1d.glsl");
	private RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();

	public FFT1DHandler() {
		Init();
	}

	public void RunFFT(float[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length * sizeof(float);

		byte[] sampleBuffer = new byte[buffSize];
		Buffer.BlockCopy(samples, 0 ,sampleBuffer, 0, (int) buffSize);
		ComputeShader.SetBuffer("Samples", sampleBuffer);
		
		ComputeShader.SetBufferSize("Frequency Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{samples.Length}, 0, pushConstants, 0, sizeof(uint));
		
		fft1d.Dispatch(rd, (uint) samples.Length, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();
		
		fft1d.GetBufferDataAsync("Frequency Domain", callback);
		rd.Submit();
		rd.Sync();
	}
	
	public void RunFFT(byte[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length;

		ComputeShader.SetBuffer("Samples", samples);
		ComputeShader.SetBufferSize("Frequency Domain", buffSize * 2);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{64}, 0, pushConstants, 0, sizeof(uint));
		
		
		fft1d.Dispatch(rd, 64, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();

		fft1d.GetBufferDataAsync("Frequency Domain", callback);
		rd.Submit();
		rd.Sync();
	}
	
	public void RunIFFT(Vector2[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length * sizeof(float) * 2;
		uint vecCount = (uint)samples.Length;

		float[] sampleFloats = new float[samples.Length * 2];
		for (int i = 0; i < samples.Length; i++) {
			sampleFloats[i * 2] = samples[i].X;
			sampleFloats[i * 2 + 1] = samples[i].Y;
		}
		byte[] sampleBuffer = new byte[buffSize];
		Buffer.BlockCopy(sampleFloats, 0 ,sampleBuffer, 0, (int) buffSize);
		ComputeShader.SetBuffer("Frequency Domain", sampleBuffer);
		
		ComputeShader.SetBufferSize("Time Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{64}, 0, pushConstants, 0, sizeof(uint));
		
		ifft1d.Dispatch(rd, 64, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();
		
		ifft1d.GetBufferDataAsync("Time Domain", callback);
		rd.Submit();
		rd.Sync();
	}
	
	public void RunIFFT(byte[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length;
		uint vecCount = (uint)samples.Length / (sizeof(float) * 2);
		
		ComputeShader.SetBuffer("Frequency Domain", samples);
		
		ComputeShader.SetBufferSize("Time Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{vecCount}, 0, pushConstants, 0, sizeof(uint));
		
		ifft1d.Dispatch(rd, 64, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();
		
		ifft1d.GetBufferDataAsync("Time Domain", callback);
		rd.Submit();
		rd.Sync();
	}

	public void RunBoth(byte[] samples, Callable callback1, Callable callback2) {
		uint buffSize = (uint) samples.Length;
		uint floatCount = (uint) samples.Length / 4;
		
		ComputeShader.SetBuffer("Samples", samples);
		ComputeShader.SetBufferSize("Frequency Domain", buffSize * 2);
		ComputeShader.SetBufferSize("Time Domain", buffSize * 2);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{floatCount}, 0, pushConstants, 0, sizeof(uint));
		
		fft1d.Dispatch(rd, floatCount, 1, 1, pushConstants);
		ifft1d.Dispatch(rd, floatCount, 1, 1, pushConstants);
		rd.Submit();
		
		rd.Sync();
		fft1d.GetBufferDataAsync("Frequency Domain", callback1);
		ifft1d.GetBufferDataAsync("Time Domain", callback2);
		rd.Submit();
		rd.Sync();
	}

	
	private void Init() {
		fft1d.CreateBuffer("Samples", RenderingDevice.UniformType.StorageBuffer, 16,  0, 0);
		fft1d.CreateBuffer("Frequency Domain", RenderingDevice.UniformType.StorageBuffer, 16, 0, 1);
		ifft1d.AssignUniform("Frequency Domain", 0, 0);
		ifft1d.CreateBuffer("Time Domain", RenderingDevice.UniformType.StorageBuffer, 16, 0, 1);
	}
	
}
