using Soenneker.Hashing.Blake3.Constants;
using Soenneker.Hashing.Blake3.Compress;
using System;
using System.Buffers.Binary;

namespace Soenneker.Hashing.Blake3.Output;

/// <summary>
/// Root output: expand root CV into output bytes (first block). Matches output_root_bytes in C.
/// </summary>
internal static class Blake3Output
{
    public static void OutputBlockFromRoot(uint[] rootCv, ulong outBlockCounter, Span<byte> dest64)
    {
        Span<uint> zeros = stackalloc uint[16];
        Span<uint> out16 = stackalloc uint[16];
        Blake3Compress.Compress(rootCv, zeros, outBlockCounter, Blake3Constants.BlockLen, Blake3Flags.Root, out16);

        for (var i = 0; i < 16; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(dest64.Slice(i * 4, 4), out16[i]);
    }
}
