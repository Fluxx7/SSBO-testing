using Godot;
using System;
using System.Text;
using GraphicsTesting.assets.Scripts.Utility;

public partial class FFT1DHandler : GodotObject {
	private ComputeHandler _compHandler;

	public FFT1DHandler() {
		_compHandler = new ComputeHandler();
		Init();
	}

	public void Refresh() {
		_compHandler.Close();
		_compHandler = new ComputeHandler();
		Init();
	}

	public void RunFFT(float[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length * sizeof(float);

		byte[] sampleBuffer = new byte[buffSize];
		Buffer.BlockCopy(samples, 0 ,sampleBuffer, 0, (int) buffSize);
		_compHandler.SetBuffer("Samples", buffSize, sampleBuffer);
		
		_compHandler.SetBuffer("Frequency Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{samples.Length}, 0, pushConstants, 0, sizeof(uint));
		
		_compHandler.DispatchSubmit("FFT", (uint) samples.Length, 1, 1, pushConstants);

		_compHandler.GetBufferDataAsync("Frequency Domain", callback);
		_compHandler.Sync();
	}
	
	public void RunFFT(byte[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length;

		_compHandler.SetBuffer("Samples", buffSize, samples);
		
		_compHandler.SetBuffer("Frequency Domain", buffSize * 2);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{64}, 0, pushConstants, 0, sizeof(uint));
		
		_compHandler.DispatchSubmit("FFT", 64, 1, 1, pushConstants);

		_compHandler.GetBufferDataAsync("Frequency Domain", callback);
		_compHandler.Sync();
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
		_compHandler.SetBuffer("Frequency Domain", buffSize, sampleBuffer);
		
		_compHandler.SetBuffer("Time Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{64}, 0, pushConstants, 0, sizeof(uint));
		
		_compHandler.DispatchSubmit("IFFT", 64, 1, 1, pushConstants);

		_compHandler.GetBufferDataAsync("Time Domain", callback);
		_compHandler.Sync();
	}
	
	public void RunIFFT(byte[] samples, Callable callback) {
		uint buffSize = (uint) samples.Length;
		uint vecCount = (uint)samples.Length / (sizeof(float) * 2);
		
		_compHandler.SetBuffer("Frequency Domain", buffSize, samples);
		
		_compHandler.SetBuffer("Time Domain", buffSize);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{vecCount}, 0, pushConstants, 0, sizeof(uint));
		
		_compHandler.DispatchSubmit("IFFT", vecCount, 1, 1, pushConstants);
		
		_compHandler.GetBufferDataAsync("Time Domain", callback);
		_compHandler.Sync();
	}

	public void RunBoth(byte[] samples, Callable callback1, Callable callback2) {
		uint buffSize = (uint) samples.Length;
		uint floatCount = (uint) samples.Length / 4;
		
		_compHandler.SetBuffer("Samples", buffSize, samples);
		_compHandler.SetBuffer("Frequency Domain", buffSize * 2);
		_compHandler.SetBuffer("Time Domain", buffSize * 2);
		
		byte[] pushConstants = new byte[16];
		Buffer.BlockCopy(new []{floatCount}, 0, pushConstants, 0, sizeof(uint));
		
		_compHandler.Dispatch("FFT", floatCount, 1, 1, pushConstants);
		_compHandler.DispatchSubmit("IFFT", floatCount, 1, 1, pushConstants);

		_compHandler.Sync();
		_compHandler.GetBufferDataAsync("Frequency Domain", callback1);
		_compHandler.GetBufferDataAsync("Time Domain", callback2);
		_compHandler.UpdateSync();
	}

	
	private void Init() {
		_compHandler.AddShader("FFT", GD.Load<RDShaderFile>("res://Scenes/Utility Tests/FFT Testing/Compute/fft_1d.glsl"));
		_compHandler.AddShader("IFFT", GD.Load<RDShaderFile>("res://Scenes/Utility Tests/FFT Testing/Compute/ifft_1d.glsl"));
		_compHandler.CreateBuffer("Samples", RenderingDevice.UniformType.StorageBuffer, 16, "FFT", 0, 0);
		_compHandler.CreateBuffer("Frequency Domain", RenderingDevice.UniformType.StorageBuffer, 16, "FFT", 0, 1);
		_compHandler.AssignUniform("IFFT", "Frequency Domain", 0, 0);
		_compHandler.CreateBuffer("Time Domain", RenderingDevice.UniformType.StorageBuffer, 16, "IFFT", 0, 1);
	}
	
}
