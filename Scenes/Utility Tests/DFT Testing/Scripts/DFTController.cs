using System;
using System.Text;
using Godot;
using GraphicsTesting.assets.Scripts.Utility;

namespace GraphicsTesting.Scenes.Utility_Tests.DFT_Testing.Scripts;

[Tool]
public partial class DFTController : Node2D {
	
	[ExportGroup("Textures")] 
	[Export] private TextureRect sampleTexRect;
	[Export] private TextureRect freqTexRect;
	[Export] private TextureRect timeTexRect;
	[ExportGroup("")]
	
	[ExportToolButton("Run 1D DFT")]
	private Callable callable1DDFT => Callable.From(RunDFT1D);
	[ExportToolButton("Run 1D IDFT")]
	private Callable callable1DIDFT => Callable.From(RunIDFT1D);

	[ExportToolButton("Run All 1D")] 
	private Callable callable1DALL => Callable.From(RunBoth1D);


	private AudioStreamWav _samples;
	private Vector2[] _freqDomain;
	private Vector2[] _timeDomain;
	
	[Export]
	private AudioStreamWav Samples {
		get => _samples;
		set {
			_samples = value; 
			_dftHandler.RunBoth(value.Data, new Callable(this, MethodName.FrequencyCallback), new Callable(this, MethodName.TimeCallback));
		}
	}

	[Export] private AudioStreamWav genSample;

	private DFT1DHandler _dftHandler = new();

	public override void _Ready() {
		RunBoth1D();
	}

	private void RunBoth1D() {
		int inputSize = _samples.Data.Length;
		float[] inFloats = new float[inputSize/4];
		Buffer.BlockCopy(_samples.Data, 0, inFloats, 0, 16);
		int printCount = Math.Min(inputSize / 4, 64);
		GD.Print("First " +  printCount + " Input Samples: ");
		StringBuilder print_string = new StringBuilder();
		for (int i = 0; i < printCount; i++) {
			print_string.Append(inFloats[i] + " + 0j, ");
		}
		GD.Print(print_string.ToString());
		_dftHandler.RunBoth(_samples.Data, new Callable(this, MethodName.FrequencyCallback), new Callable(this, MethodName.TimeCallback));
	}
	
	private void RunDFT1D() {
		if (_samples == null) {
			return;
		}
		int inputSize = _samples.Data.Length;
		float[] inFloats = new float[inputSize/4];
		Buffer.BlockCopy(_samples.Data, 0, inFloats, 0, 16);
		GD.Print("First " +  inputSize / 4 + " Input Samples: ");
		StringBuilder print_string = new StringBuilder();
		for (int i = 0; i < inputSize / 4; i++) {
			print_string.Append(inFloats[i] + " + 0j, ");
		}
		GD.Print(print_string.ToString());
		_dftHandler.RunDFT(_samples.Data, new Callable(this, MethodName.FrequencyCallback));
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
	
	private void RunIDFT1D() {
		if (_freqDomain == null) {
			return;
		}
		_dftHandler.RunIDFT(_freqDomain, new Callable(this, MethodName.TimeCallback));
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