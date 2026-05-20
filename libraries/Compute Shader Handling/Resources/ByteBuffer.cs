using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace GraphicsTesting.Libraries.ComputeShaderHandling;

public partial class ByteBuffer() : RefCounted {
	private List<byte[]> bytes = [];
	private ulong size;
	
	
	public ByteBuffer(uint val) : this() {
		Add(val);
	}

	public ByteBuffer Add<T>(T val) where T : unmanaged {
		Span<T> span = stackalloc T[1];
		span[0] = val;
		byte[] val_bytes = MemoryMarshal.AsBytes(span).ToArray();
		bytes.Add(val_bytes);
		size += (ulong) val_bytes.Length;
		return this;
	}
	
	public ByteBuffer Add<T>(T[] values) where T : unmanaged {
		Span<byte> bytes = MemoryMarshal.AsBytes(values.AsSpan());
		this.bytes.Add(bytes.ToArray());
		size += (ulong)bytes.Length;
		return this;
	}

	public byte[] Generate() {
		byte[] output = new byte[size];
		uint curr_offset = 0;
		foreach (byte[] arr in bytes) {
			foreach (byte val in arr) {
				output[curr_offset] = val;
				curr_offset++;
			}
		}

		return output;
	}
	
	public byte[] Generate(uint padding) {
		ulong out_size = size;
		if (out_size % padding != 0) {
			out_size += padding - (out_size % padding);
		} 
		byte[] output = new byte[out_size];
		uint curr_offset = 0;
		foreach (byte[] arr in bytes) {
			foreach (byte val in arr) {
				output[curr_offset] = val;
				curr_offset++;
			}
		}

		return output;
	}

}