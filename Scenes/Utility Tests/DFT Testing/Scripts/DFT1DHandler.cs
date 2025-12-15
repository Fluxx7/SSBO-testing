using Godot;
using System;
using System.Text;
using GraphicsTesting.assets.Scripts.Utility;
using GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class DFT1DHandler : GodotObject {
	private ComputeShader dft1D = new("res://Scenes/Utility Tests/DFT Testing/Compute/dft_1d.glsl");
	private ComputeShader idft1d = new("res://Scenes/Utility Tests/DFT Testing/Compute/idft_1d.glsl");
	private RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();

	public DFT1DHandler() {
		Init();
	}

	public void RunDFT(float[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length * sizeof(float);

		byte[] sampleBuffer = new byte[buffSize];
		Buffer.BlockCopy(samples, 0 ,sampleBuffer, 0, (int) buffSize);
		ComputeShader.SetBuffer("Samples", sampleBuffer);
		
		ComputeShader.SetBufferSize("Frequency Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{samples.Length}, 0, pushConstants, 0, sizeof(uint));
		
		dft1D.Dispatch(rd, (uint) samples.Length, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();
		
		dft1D.GetBufferDataAsync("Frequency Domain", callback);
		rd.Submit();
		rd.Sync();
	}
	
	public void RunDFT(byte[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length;

		ComputeShader.SetBuffer("Samples", samples);
		ComputeShader.SetBufferSize("Frequency Domain", buffSize * 2);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{64}, 0, pushConstants, 0, sizeof(uint));
		
		
		dft1D.Dispatch(rd, 64, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();

		dft1D.GetBufferDataAsync("Frequency Domain", callback);
		rd.Submit();
		rd.Sync();
	}
	
	public void RunIDFT(Vector2[] samples, Callable callback) {
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
		
		idft1d.Dispatch(rd, 64, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();
		
		idft1d.GetBufferDataAsync("Time Domain", callback);
		rd.Submit();
		rd.Sync();
	}
	
	public void RunIDFT(byte[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length;
		uint vecCount = (uint)samples.Length / (sizeof(float) * 2);
		
		ComputeShader.SetBuffer("Frequency Domain", samples);
		
		ComputeShader.SetBufferSize("Time Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{vecCount}, 0, pushConstants, 0, sizeof(uint));
		
		idft1d.Dispatch(rd, 64, 1, 1, pushConstants);
		rd.Submit();
		rd.Sync();
		
		idft1d.GetBufferDataAsync("Time Domain", callback);
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
		
		dft1D.Dispatch(rd, floatCount, 1, 1, pushConstants);
		idft1d.Dispatch(rd, floatCount, 1, 1, pushConstants);
		rd.Submit();
		
		rd.Sync();
		dft1D.GetBufferDataAsync("Frequency Domain", callback1);
		idft1d.GetBufferDataAsync("Time Domain", callback2);
		rd.Submit();
		rd.Sync();
	}

	
	private void Init() {
		dft1D.CreateBuffer("Samples", RenderingDevice.UniformType.StorageBuffer, 16,  0, 0);
		dft1D.CreateBuffer("Frequency Domain", RenderingDevice.UniformType.StorageBuffer, 16, 0, 1);
		idft1d.AssignUniform("Frequency Domain", 0, 0);
		idft1d.CreateBuffer("Time Domain", RenderingDevice.UniformType.StorageBuffer, 16, 0, 1);
	}
	
}
