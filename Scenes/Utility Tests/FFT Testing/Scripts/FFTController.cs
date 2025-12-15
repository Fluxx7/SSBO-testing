using System;
using System.Text;
using Godot;
using GraphicsTesting.assets.Scripts.Utility;

namespace GraphicsTesting.Scenes.Utility_Tests.DFT_Testing.Scripts;

[Tool]
public partial class FFTController : Node2D {
	
	[ExportGroup("Textures")] 
	[Export] private TextureRect sampleTexRect;
	[Export] private TextureRect freqTexRect;
	[Export] private TextureRect timeTexRect;
	[ExportGroup("")]
	
	[ExportToolButton("Run 1D FFT")]
	private Callable callable1DDFT => Callable.From(RunFFT1D);
	[ExportToolButton("Run 1D IFFT")]
	private Callable callable1DIDFT => Callable.From(RunIFFT1D);

	[ExportToolButton("Run All 1D")] 
	private Callable callable1DALL => Callable.From(RunBoth1D);
	
	private Vector2[] _freqDomain;
	private Vector2[] _timeDomain;
	private byte[] _samples;

	[Export] private AudioStreamWav genSample;

	private FFT1DHandler _fftHandler = new();

	public override void _Ready() {
		RunBoth1D();
	}

	private void RunBoth1D() {
		int inputSize = _samples.Length;
		float[] inFloats = new float[inputSize/4];
		Buffer.BlockCopy(_samples, 0, inFloats, 0, 16);
		int printCount = Math.Min(inputSize / 4, 64);
		GD.Print("First " +  printCount + " Input Samples: ");
		StringBuilder print_string = new StringBuilder();
		for (int i = 0; i < printCount; i++) {
			print_string.Append(inFloats[i] + " + 0j, ");
		}
		GD.Print(print_string.ToString());
		_fftHandler.RunBoth(_samples, new Callable(this, MethodName.FrequencyCallback), new Callable(this, MethodName.TimeCallback));
	}
	
	private void RunFFT1D() {
		if (_samples == null) {
			return;
		}
		int inputSize = _samples.Length;
		float[] inFloats = new float[inputSize/4];
		Buffer.BlockCopy(_samples, 0, inFloats, 0, 16);
		GD.Print("First " +  inputSize / 4 + " Input Samples: ");
		StringBuilder print_string = new StringBuilder();
		for (int i = 0; i < inputSize / 4; i++) {
			print_string.Append(inFloats[i] + " + 0j, ");
		}
		GD.Print(print_string.ToString());
		_fftHandler.RunFFT(_samples, new Callable(this, MethodName.FrequencyCallback));
	}

	private void FrequencyCallback(byte[] data) {
		int floatCount = data.Length / 4;
		float[] outFloats = new float[floatCount];
		Buffer.BlockCopy(data, 0, outFloats, 0, data.Length);
		int printCount = Math.Min(floatCount, 64);
		GD.Print("First " + printCount + " Frequency Samples: ");
		StringBuilder print_string = new StringBuilder();
		for (int i = 0; i < printCount; i++) {
			print_string.Append(outFloats[2*i] + " + " + outFloats[2*i +1] + "j, ");
		}
		GD.Print(print_string.ToString());
		_freqDomain = new Vector2[data.Length / 8];
		for (int i = 0; i < data.Length / 8; i ++) {
			_freqDomain[i] = new Vector2(outFloats[i*2], outFloats[i*2 + 1]);
		}
	}
	
	private void RunIFFT1D() {
		if (_freqDomain == null) {
			return;
		}
		_fftHandler.RunIFFT(_freqDomain, new Callable(this, MethodName.TimeCallback));
	}

	private void TimeCallback(byte[] data) {
		int floatCount = data.Length / 4;
		float[] outFloats = new float[floatCount];
		Buffer.BlockCopy(data, 0, outFloats, 0, data.Length);
		_timeDomain = new Vector2[data.Length / 8];
		float[] outSamples = new float[data.Length / 8];
		for (int i = 0; i < data.Length / 8; i ++) {
			_timeDomain[i] = new Vector2(outFloats[i*2], outFloats[i*2 + 1]);
			outSamples[i] = outFloats[i*2];
		}
		
		byte[] outdata = new byte[outSamples.Length * 4];
		Buffer.BlockCopy(outSamples, 0, outdata, 0, outdata.Length);
		int printCount = Math.Min(floatCount, 64);
		GD.Print("First " + printCount + " Output Samples: ");
		StringBuilder print_string = new StringBuilder();
		for (int i = 0; i < printCount; i++) {
			print_string.Append(outFloats[2*i] + " + " + outFloats[2*i +1] + "j, ");
		}
		GD.Print(print_string.ToString());
		genSample = new AudioStreamWav();
		genSample.Data = outdata;
	}
}