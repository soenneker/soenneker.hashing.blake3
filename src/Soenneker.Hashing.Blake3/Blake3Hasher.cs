using Soenneker.Hashing.Blake3.Constants;
using Soenneker.Hashing.Blake3.ChunkState;
using Soenneker.Hashing.Blake3.Compress;
using Soenneker.Hashing.Blake3.Parent;
using Soenneker.Hashing.Blake3.Simd;
using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using Soenneker.Extensions.Arrays.Bytes;

namespace Soenneker.Hashing.Blake3;

/// <summary>
/// Public API for BLAKE3 hashing. Delegates to compress, chunk state, output, parent, and SIMD modules.
/// </summary>
public static partial class Blake3Hasher
{
    private const int _parallelInputThreshold = 4096;
    private const int _parallelMinChunks = 64;

    /// <summary>
    /// Computes the BLAKE3 hash of the input and returns a 32-byte digest.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
    [Pure]
    public static byte[] Hash(byte[] input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var output = new byte[Blake3Constants.OutLen];
        Hash((ReadOnlySpan<byte>)input, output);
        return output;
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input and returns a 32-byte digest.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    [Pure]
    public static byte[] Hash(ReadOnlySpan<byte> input)
    {
        var output = new byte[Blake3Constants.OutLen];
        Hash(input, output);
        return output;
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input and returns a 32-byte digest.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    public static byte[] Hash(ReadOnlyMemory<byte> input)
    {
        var output = new byte[Blake3Constants.OutLen];
        Hash(input, output);
        return output;
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input and writes the 32-byte digest to <paramref name="destination"/>.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <param name="destination">The span to write the 32-byte digest into. Must be at least 32 bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
    public static void Hash(byte[] input, Span<byte> destination)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        if (input.Length >= Blake3Constants.ChunkLen * _parallelMinChunks)
        {
            HashParallel(input, destination);
            return;
        }

        Hash((ReadOnlySpan<byte>)input, destination);
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input and writes the 32-byte digest to <paramref name="destination"/>.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <param name="destination">The span to write the 32-byte digest into. Must be at least 32 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> has length less than 32.</exception>
    public static void Hash(ReadOnlySpan<byte> input, Span<byte> destination)
    {
        if (destination.Length < Blake3Constants.OutLen)
            throw new ArgumentException($"Destination must be at least {Blake3Constants.OutLen} bytes.", nameof(destination));

        if (input.Length <= Blake3Constants.ChunkLen)
        {
            Blake3ChunkState.ChunkRoot32(input, 0, 0, destination);
            return;
        }

        HashSimdSequential(input, destination);
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input and writes the 32-byte digest to <paramref name="destination"/>.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <param name="destination">The span to write the 32-byte digest into. Must be at least 32 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> has length less than 32.</exception>
    [Pure]
    public static void Hash(ReadOnlyMemory<byte> input, Span<byte> destination)
    {
        if (destination.Length < Blake3Constants.OutLen)
            throw new ArgumentException($"Destination must be at least {Blake3Constants.OutLen} bytes.", nameof(destination));

        if (input.Length >= Blake3Constants.ChunkLen * _parallelMinChunks)
        {
            HashParallel(input, destination);
            return;
        }

        Hash(input.Span, destination);
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input using a copy of the data. Use for large inputs when parallel hashing is desired.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    [Pure]
    public static byte[] HashParallelCopy(ReadOnlySpan<byte> input) => Hash(input.ToArray());

    /// <summary>
    /// Computes the BLAKE3 hash of the string encoded as UTF-8 and returns a 32-byte digest.
    /// </summary>
    /// <param name="s">The string to hash (encoded as UTF-8).</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    [Pure]
    public static byte[] Hash(string s) => Hash((ReadOnlySpan<char>)s);

    /// <summary>
    /// Hashes the input string (UTF-8) and returns the hash as a lowercase hex string.
    /// </summary>
    /// <param name="input">The string to hash (encoded as UTF-8).</param>
    /// <returns>A 64-character lowercase hex string of the 32-byte BLAKE3 digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
    [Pure]
    public static string HashToString(string input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        byte[] hash = Hash(input);

        return hash.ToHexLower();
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the character span encoded as UTF-8 and returns a 32-byte digest.
    /// </summary>
    /// <param name="chars">The characters to hash (encoded as UTF-8).</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    [Pure]
    public static byte[] Hash(ReadOnlySpan<char> chars)
    {
        int maxBytes = System.Text.Encoding.UTF8.GetMaxByteCount(chars.Length);
        var output = new byte[Blake3Constants.OutLen];

        if (maxBytes <= _parallelInputThreshold)
        {
            Span<byte> buf = stackalloc byte[maxBytes];
            int n = System.Text.Encoding.UTF8.GetBytes(chars, buf);
            Hash(buf[..n], output);
            return output;
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            int n = System.Text.Encoding.UTF8.GetBytes(chars, rented);
            Hash(rented.AsSpan(0, n), output);
            return output;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input using parallel processing when the input is large enough.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
    [Pure]
    public static byte[] HashParallel(byte[] input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var output = new byte[Blake3Constants.OutLen];
        HashParallel((ReadOnlyMemory<byte>)input, output);
        return output;
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input using parallel processing when the input is large enough.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash.</returns>
    [Pure]
    public static byte[] HashParallel(ReadOnlyMemory<byte> input)
    {
        var output = new byte[Blake3Constants.OutLen];
        HashParallel(input, output);
        return output;
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input using parallel processing and writes the 32-byte digest to <paramref name="destination"/>.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <param name="destination">The span to write the 32-byte digest into. Must be at least 32 bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
    public static void HashParallel(byte[] input, Span<byte> destination)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        HashParallel((ReadOnlyMemory<byte>)input, destination);
    }

    /// <summary>
    /// Computes the BLAKE3 hash of the input using parallel processing and writes the 32-byte digest to <paramref name="destination"/>.
    /// </summary>
    /// <param name="input">The data to hash.</param>
    /// <param name="destination">The span to write the 32-byte digest into. Must be at least 32 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> has length less than 32.</exception>
    public static void HashParallel(ReadOnlyMemory<byte> input, Span<byte> destination)
    {
        if (destination.Length < Blake3Constants.OutLen)
            throw new ArgumentException($"Destination must be at least {Blake3Constants.OutLen} bytes.", nameof(destination));

        ReadOnlySpan<byte> span = input.Span;
        int chunkCount = GetChunkCount(span.Length);
        if (chunkCount < _parallelMinChunks)
        {
            HashSimdSequential(span, destination);
            return;
        }

        uint[] rented = ArrayPool<uint>.Shared.Rent(chunkCount * 8);
        bool avx2 = Avx2.IsSupported;
        bool neon = !avx2 && AdvSimd.IsSupported;
        int fullChunkCount = span.Length / Blake3Constants.ChunkLen;

        try
        {
            int workers = Math.Min(Environment.ProcessorCount, chunkCount);
            int perWorker = (chunkCount + workers - 1) / workers;
            Parallel.For(0, workers, w =>
            {
                int start = w * perWorker;
                int end = Math.Min(chunkCount, start + perWorker);
                if (start >= end)
                    return;

                ReadOnlySpan<byte> local = input.Span;
                int i = start;

                if (avx2)
                {
                    for (; i + 8 <= end; i += 8)
                    {
                        if (i + 8 <= fullChunkCount)
                        {
                            Blake3Avx2.ChunkToCvBatch(local, i, rented.AsSpan(i * 8, 64));
                            continue;
                        }

                        for (var k = 0; k < 8; k++)
                        {
                            int chunk = i + k;
                            Blake3ChunkState.ChunkToCv(Blake3ChunkState.GetChunkSlice(local, chunk), (ulong)chunk, 0, rented.AsSpan(chunk * 8, 8));
                        }
                    }
                }
                else if (neon)
                {
                    for (; i + 4 <= end; i += 4)
                    {
                        if (i + 4 <= fullChunkCount)
                        {
                            Blake3Neon.ChunkToCvBatch(local, i, rented.AsSpan(i * 8, 32));
                            continue;
                        }

                        for (var k = 0; k < 4; k++)
                        {
                            int chunk = i + k;
                            Blake3ChunkState.ChunkToCv(Blake3ChunkState.GetChunkSlice(local, chunk), (ulong)chunk, 0, rented.AsSpan(chunk * 8, 8));
                        }
                    }
                }

                for (; i < end; i++)
                    Blake3ChunkState.ChunkToCv(Blake3ChunkState.GetChunkSlice(local, i), (ulong)i, 0, rented.AsSpan(i * 8, 8));
            });

            FinalizeHashFromChunkCvs(rented.AsSpan(0, chunkCount * 8), chunkCount, destination);
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void HashSimdSequential(ReadOnlySpan<byte> input, Span<byte> destination)
    {
        int chunkCount = GetChunkCount(input.Length);
        if (chunkCount == 1)
        {
            Blake3ChunkState.ChunkRoot32(input, 0, 0, destination);
            return;
        }

        Span<uint> cvStack = stackalloc uint[64 * 8];
        var cvStackLen = 0;
        Span<uint> parentBlock = stackalloc uint[16];
        Span<uint> leftCv = stackalloc uint[8];
        Span<uint> rightCv = stackalloc uint[8];
        Span<uint> chunkCv = stackalloc uint[8];
        Span<uint> batchCvs = stackalloc uint[64];

        int fullChunkCount = input.Length / Blake3Constants.ChunkLen;
        int chunksBeforeLast = chunkCount - 1;
        var i = 0;

        if (Avx2.IsSupported)
        {
            for (; i + 8 <= chunksBeforeLast && i + 8 <= fullChunkCount; i += 8)
            {
                Blake3Avx2.ChunkToCvBatch(input, i, batchCvs);

                for (var k = 0; k < 8; k++)
                    AddChunkCv(cvStack, ref cvStackLen, batchCvs.Slice(k * 8, 8), (ulong)(i + k + 1), parentBlock, leftCv, rightCv);
            }
        }
        else if (AdvSimd.IsSupported)
        {
            for (; i + 4 <= chunksBeforeLast && i + 4 <= fullChunkCount; i += 4)
            {
                Blake3Neon.ChunkToCvBatch(input, i, batchCvs);

                for (var k = 0; k < 4; k++)
                    AddChunkCv(cvStack, ref cvStackLen, batchCvs.Slice(k * 8, 8), (ulong)(i + k + 1), parentBlock, leftCv, rightCv);
            }
        }

        for (; i < chunksBeforeLast; i++)
        {
            Blake3ChunkState.ChunkToCv(Blake3ChunkState.GetChunkSlice(input, i), (ulong)i, 0, chunkCv);
            AddChunkCv(cvStack, ref cvStackLen, chunkCv, (ulong)(i + 1), parentBlock, leftCv, rightCv);
        }

        int lastChunk = chunkCount - 1;
        Blake3ChunkState.ChunkToCv(Blake3ChunkState.GetChunkSlice(input, lastChunk), (ulong)lastChunk, 0, chunkCv);

        int stackIdx = cvStackLen;
        while (stackIdx > 0)
        {
            stackIdx--;
            cvStack.Slice(stackIdx * 8, 8).CopyTo(leftCv);
            leftCv.CopyTo(parentBlock.Slice(0, 8));
            chunkCv.CopyTo(parentBlock.Slice(8, 8));

            if (stackIdx == 0)
            {
                Blake3Compress.CompressRoot32(Blake3Constants.Iv, parentBlock, 0, Blake3Constants.BlockLen, Blake3Flags.Parent | Blake3Flags.Root, destination);
                return;
            }

            Blake3Compress.CompressCv(Blake3Constants.Iv, parentBlock, 0, Blake3Constants.BlockLen, Blake3Flags.Parent, chunkCv);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushCv(Span<uint> cvStack, ref int cvStackLen, ReadOnlySpan<uint> cv)
    {
        cv.CopyTo(cvStack.Slice(cvStackLen * 8, 8));
        cvStackLen++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PopCv(Span<uint> cvStack, ref int cvStackLen, Span<uint> cv)
    {
        cvStackLen--;
        cvStack.Slice(cvStackLen * 8, 8).CopyTo(cv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddChunkCv(Span<uint> cvStack, ref int cvStackLen, ReadOnlySpan<uint> newCv, ulong totalChunks, Span<uint> parentBlock, Span<uint> leftCv, Span<uint> rightCv)
    {
        newCv.CopyTo(rightCv);
        ulong tc = totalChunks;

        while ((tc & 1) == 0)
        {
            PopCv(cvStack, ref cvStackLen, leftCv);
            leftCv.CopyTo(parentBlock.Slice(0, 8));
            rightCv.CopyTo(parentBlock.Slice(8, 8));
            Blake3Compress.CompressCv(Blake3Constants.Iv, parentBlock, 0, Blake3Constants.BlockLen, Blake3Flags.Parent, rightCv);
            tc >>= 1;
        }

        PushCv(cvStack, ref cvStackLen, rightCv);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void HashScalar(ReadOnlySpan<byte> input, Span<byte> destination)
    {
        int chunkCount = GetChunkCount(input.Length);

        if (chunkCount == 1)
        {
            Blake3ChunkState.ChunkRoot32(input, 0, 0, destination);
            return;
        }

        uint[] rented = ArrayPool<uint>.Shared.Rent(chunkCount * 8);
        try
        {
            for (var c = 0; c < chunkCount; c++)
                Blake3ChunkState.ChunkToCv(Blake3ChunkState.GetChunkSlice(input, c), (ulong)c, 0, rented.AsSpan(c * 8, 8));

            FinalizeHashFromChunkCvs(rented.AsSpan(0, chunkCount * 8), chunkCount, destination);
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(rented);
        }
    }

    private static int GetChunkCount(int inputLength)
    {
        int chunkCount = (inputLength + Blake3Constants.ChunkLen - 1) / Blake3Constants.ChunkLen;
        return chunkCount == 0 ? 1 : chunkCount;
    }

    private static void FinalizeHashFromChunkCvs(Span<uint> chunkCvs, int chunkCount, Span<byte> destination)
    {
        int count = chunkCount;

        while (count > 2)
        {
            int pairs = count / 2;

            for (var p = 0; p < pairs; p++)
            {
                int left = p * 2 * 8;
                int right = left + 8;
                int dest = p * 8;
                Blake3Parent.ParentCv(chunkCvs.Slice(left, 8), chunkCvs.Slice(right, 8), Blake3Constants.Iv, 0, chunkCvs.Slice(dest, 8));
            }

            if ((count & 1) == 1)
            {
                int src = (count - 1) * 8;
                int dst = pairs * 8;
                if (src != dst)
                    chunkCvs.Slice(src, 8).CopyTo(chunkCvs.Slice(dst, 8));
                count = pairs + 1;
            }
            else
            {
                count = pairs;
            }
        }

        Blake3Parent.ParentRoot32(chunkCvs.Slice(0, 8), chunkCvs.Slice(8, 8), Blake3Constants.Iv, 0, destination);
    }
}
