using Soenneker.Hashing.Blake3.Constants;
using Soenneker.Hashing.Blake3.Compress;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Soenneker.Hashing.Blake3.ChunkState;

/// <summary>
/// Chunk-level hashing: process one 1024-byte chunk into a chaining value (8 words).
/// Matches chunk_state_* in the C implementation.
/// </summary>
internal static class Blake3ChunkState
{
    public static ReadOnlySpan<byte> GetChunkSlice(ReadOnlySpan<byte> input, int chunkIndex)
    {
        int off = chunkIndex * Blake3Constants.ChunkLen;

        if (off >= input.Length)
            return ReadOnlySpan<byte>.Empty;

        int len = Math.Min(Blake3Constants.ChunkLen, input.Length - off);
        return input.Slice(off, len);
    }

    /// <summary>
    /// Hash one chunk (possibly partial) to an 8-word chaining value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ChunkToCv(ReadOnlySpan<byte> chunk, ulong chunkCounter, uint flags, Span<uint> destCv8)
    {
        if (destCv8.Length < 8)
            throw new ArgumentException("Destination CV span must be at least 8 uints.", nameof(destCv8));

        Span<uint> cv = stackalloc uint[8];

        for (var i = 0; i < 8; i++)
            cv[i] = Blake3Constants.Iv[i];

        Span<byte> tailBlock = stackalloc byte[Blake3Constants.BlockLen];
        Span<uint> swapped = stackalloc uint[16];
        bool littleEndian = BitConverter.IsLittleEndian;

        if (chunk.IsEmpty)
        {
            tailBlock.Clear();
            Span<uint> mEmpty = MemoryMarshal.Cast<byte, uint>(tailBlock);
            uint emptyFlags = flags | Blake3Flags.ChunkStart | Blake3Flags.ChunkEnd;
            Blake3Compress.CompressCv(cv, mEmpty, chunkCounter, 0, emptyFlags, cv);
        }
        else
        {
            int fullBlocks = chunk.Length / Blake3Constants.BlockLen;
            int remainder = chunk.Length - fullBlocks * Blake3Constants.BlockLen;

            for (var blockIndex = 0; blockIndex < fullBlocks; blockIndex++)
            {
                ReadOnlySpan<byte> blockBytes = chunk.Slice(blockIndex * Blake3Constants.BlockLen, Blake3Constants.BlockLen);
                bool isStart = blockIndex == 0;
                bool isEnd = remainder == 0 && blockIndex == fullBlocks - 1;
                uint blockFlags = flags | (isStart ? Blake3Flags.ChunkStart : 0) | (isEnd ? Blake3Flags.ChunkEnd : 0);

                if (littleEndian)
                {
                    ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(blockBytes);
                    Blake3Compress.CompressCv(cv, m, chunkCounter, Blake3Constants.BlockLen, blockFlags, cv);
                }
                else
                {
                    ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(blockBytes);
                    for (var i = 0; i < 16; i++)
                        swapped[i] = BinaryPrimitives.ReverseEndianness(m[i]);
                    Blake3Compress.CompressCv(cv, swapped, chunkCounter, Blake3Constants.BlockLen, blockFlags, cv);
                }
            }

            if (remainder > 0)
            {
                tailBlock.Clear();
                chunk.Slice(fullBlocks * Blake3Constants.BlockLen, remainder)
                     .CopyTo(tailBlock);

                bool isStart = fullBlocks == 0;
                uint blockFlags = flags | (isStart ? Blake3Flags.ChunkStart : 0) | Blake3Flags.ChunkEnd;
                if (littleEndian)
                {
                    ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(tailBlock);
                    Blake3Compress.CompressCv(cv, m, chunkCounter, (uint)remainder, blockFlags, cv);
                }
                else
                {
                    ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(tailBlock);
                    for (var i = 0; i < 16; i++)
                        swapped[i] = BinaryPrimitives.ReverseEndianness(m[i]);
                    Blake3Compress.CompressCv(cv, swapped, chunkCounter, (uint)remainder, blockFlags, cv);
                }
            }
        }

        for (var i = 0; i < 8; i++)
            destCv8[i] = cv[i];
    }

    /// <summary>
    /// Hash one chunk and emit root bytes directly from the final chunk output state.
    /// This is required for the single-chunk root case in BLAKE3.
    /// </summary>
    public static void ChunkRoot32(ReadOnlySpan<byte> chunk, ulong chunkCounter, uint flags, Span<byte> destination32)
    {
        Span<uint> cv = stackalloc uint[8];
        for (var i = 0; i < 8; i++)
            cv[i] = Blake3Constants.Iv[i];

        Span<byte> tailBlock = stackalloc byte[Blake3Constants.BlockLen];
        Span<uint> swapped = stackalloc uint[16];
        Span<uint> out16 = stackalloc uint[16];
        bool littleEndian = BitConverter.IsLittleEndian;

        if (chunk.IsEmpty)
        {
            tailBlock.Clear();
            ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(tailBlock);
            uint blockFlags = flags | Blake3Flags.ChunkStart | Blake3Flags.ChunkEnd;
            Blake3Compress.CompressRoot32(cv, m, chunkCounter, 0, blockFlags | Blake3Flags.Root, destination32);
            return;
        }

        int fullBlocks = chunk.Length / Blake3Constants.BlockLen;
        int remainder = chunk.Length - fullBlocks * Blake3Constants.BlockLen;

        for (var blockIndex = 0; blockIndex < fullBlocks; blockIndex++)
        {
            ReadOnlySpan<byte> blockBytes = chunk.Slice(blockIndex * Blake3Constants.BlockLen, Blake3Constants.BlockLen);
            bool isStart = blockIndex == 0;
            bool isEnd = remainder == 0 && blockIndex == fullBlocks - 1;
            uint blockFlags = flags | (isStart ? Blake3Flags.ChunkStart : 0) | (isEnd ? Blake3Flags.ChunkEnd : 0);

            if (isEnd)
            {
                if (littleEndian)
                {
                    ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(blockBytes);
                    Blake3Compress.CompressRoot32(cv, m, chunkCounter, Blake3Constants.BlockLen, blockFlags | Blake3Flags.Root, destination32);
                }
                else
                {
                    ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(blockBytes);
                    for (var i = 0; i < 16; i++)
                        swapped[i] = BinaryPrimitives.ReverseEndianness(m[i]);
                    Blake3Compress.CompressRoot32(cv, swapped, chunkCounter, Blake3Constants.BlockLen, blockFlags | Blake3Flags.Root, destination32);
                }
                return;
            }

            if (littleEndian)
            {
                ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(blockBytes);
                Blake3Compress.Compress(cv, m, chunkCounter, Blake3Constants.BlockLen, blockFlags, out16);
            }
            else
            {
                ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(blockBytes);
                for (var i = 0; i < 16; i++)
                    swapped[i] = BinaryPrimitives.ReverseEndianness(m[i]);
                Blake3Compress.Compress(cv, swapped, chunkCounter, Blake3Constants.BlockLen, blockFlags, out16);
            }
            out16[..8].CopyTo(cv);
        }

        if (remainder > 0)
        {
            tailBlock.Clear();
            chunk.Slice(fullBlocks * Blake3Constants.BlockLen, remainder)
                 .CopyTo(tailBlock);

            uint blockFlags = flags | (fullBlocks == 0 ? Blake3Flags.ChunkStart : 0) | Blake3Flags.ChunkEnd;
            if (littleEndian)
            {
                ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(tailBlock);
                Blake3Compress.CompressRoot32(cv, m, chunkCounter, (uint)remainder, blockFlags | Blake3Flags.Root, destination32);
            }
            else
            {
                ReadOnlySpan<uint> m = MemoryMarshal.Cast<byte, uint>(tailBlock);
                for (var i = 0; i < 16; i++)
                    swapped[i] = BinaryPrimitives.ReverseEndianness(m[i]);
                Blake3Compress.CompressRoot32(cv, swapped, chunkCounter, (uint)remainder, blockFlags | Blake3Flags.Root, destination32);
            }
        }
    }
}
