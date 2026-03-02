using Soenneker.Hashing.Blake3.Constants;
using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Soenneker.Hashing.Blake3.Compress;

/// <summary>
/// Portable (scalar) BLAKE3 compression core. Matches blake3_compress_in_place in C/Rust.
/// </summary>
internal static class Blake3Compress
{
    private static readonly byte[] _mIndexFlat =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8,
        3, 4, 10, 12, 13, 2, 7, 14, 6, 5, 9, 0, 11, 15, 8, 1,
        10, 7, 12, 9, 14, 3, 13, 15, 4, 0, 11, 2, 5, 8, 1, 6,
        12, 13, 9, 11, 15, 10, 14, 8, 7, 2, 5, 3, 0, 1, 6, 4,
        9, 14, 11, 5, 8, 12, 15, 1, 13, 3, 0, 10, 2, 6, 4, 7,
        11, 15, 5, 0, 1, 9, 8, 6, 14, 10, 2, 12, 3, 4, 7, 13
    ];

    /// <summary>
    /// Core compression: one block (64 bytes) in, 16 words out. Used for chunks, parents, and root output.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compress(ReadOnlySpan<uint> cv8, ReadOnlySpan<uint> m16, ulong counter, uint blockLen, uint flags, Span<uint> out16)
    {
        if (Blake3CompressSse41.IsSupported)
        {
            Blake3CompressSse41.Compress(cv8, m16, counter, blockLen, flags, out16);
            return;
        }

        CompressScalar(cv8, m16, counter, blockLen, flags, out16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CompressScalar(ReadOnlySpan<uint> cv8, ReadOnlySpan<uint> m16, ulong counter, uint blockLen, uint flags, Span<uint> out16)
    {
        Span<uint> v = stackalloc uint[16];

        v[0] = cv8[0];
        v[1] = cv8[1];
        v[2] = cv8[2];
        v[3] = cv8[3];
        v[4] = cv8[4];
        v[5] = cv8[5];
        v[6] = cv8[6];
        v[7] = cv8[7];

        v[8] = Blake3Constants.Iv[0];
        v[9] = Blake3Constants.Iv[1];
        v[10] = Blake3Constants.Iv[2];
        v[11] = Blake3Constants.Iv[3];

        v[12] = (uint)counter;
        v[13] = (uint)(counter >> 32);
        v[14] = blockLen;
        v[15] = flags;

        for (var r = 0; r < Blake3Constants.Rounds; r++)
        {
            int offset = r << 4;

            G(ref v[0], ref v[4], ref v[8], ref v[12], m16[_mIndexFlat[offset]], m16[_mIndexFlat[offset + 1]]);
            G(ref v[1], ref v[5], ref v[9], ref v[13], m16[_mIndexFlat[offset + 2]], m16[_mIndexFlat[offset + 3]]);
            G(ref v[2], ref v[6], ref v[10], ref v[14], m16[_mIndexFlat[offset + 4]], m16[_mIndexFlat[offset + 5]]);
            G(ref v[3], ref v[7], ref v[11], ref v[15], m16[_mIndexFlat[offset + 6]], m16[_mIndexFlat[offset + 7]]);

            G(ref v[0], ref v[5], ref v[10], ref v[15], m16[_mIndexFlat[offset + 8]], m16[_mIndexFlat[offset + 9]]);
            G(ref v[1], ref v[6], ref v[11], ref v[12], m16[_mIndexFlat[offset + 10]], m16[_mIndexFlat[offset + 11]]);
            G(ref v[2], ref v[7], ref v[8], ref v[13], m16[_mIndexFlat[offset + 12]], m16[_mIndexFlat[offset + 13]]);
            G(ref v[3], ref v[4], ref v[9], ref v[14], m16[_mIndexFlat[offset + 14]], m16[_mIndexFlat[offset + 15]]);
        }

        for (var i = 0; i < 8; i++)
        {
            out16[i] = v[i] ^ v[8 + i];
            out16[8 + i] = v[8 + i] ^ cv8[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CompressCv(ReadOnlySpan<uint> cv8, ReadOnlySpan<uint> m16, ulong counter, uint blockLen, uint flags, Span<uint> destinationCv8)
    {
        if (destinationCv8.Length < 8)
            throw new ArgumentException("Destination CV span must be at least 8 uints.", nameof(destinationCv8));

        Span<uint> out16 = stackalloc uint[16];
        Compress(cv8, m16, counter, blockLen, flags, out16);
        out16[..8]
            .CopyTo(destinationCv8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CompressRoot32(ReadOnlySpan<uint> cv8, ReadOnlySpan<uint> m16, ulong counter, uint blockLen, uint flags, Span<byte> destination32)
    {
        if (destination32.Length < Blake3Constants.OutLen)
            throw new ArgumentException($"Destination must be at least {Blake3Constants.OutLen} bytes.", nameof(destination32));

        Span<uint> v = stackalloc uint[16];
        Compress(cv8, m16, counter, blockLen, flags, v);

        for (var i = 0; i < 8; i++)
        {
            uint word = v[i];
            BinaryPrimitives.WriteUInt32LittleEndian(destination32.Slice(i * 4, 4), word);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void G(ref uint a, ref uint b, ref uint c, ref uint d, uint mx, uint my)
    {
        a = unchecked(a + b + mx);
        d = RotateRight32(d ^ a, 16);
        c = unchecked(c + d);
        b = RotateRight32(b ^ c, 12);
        a = unchecked(a + b + my);
        d = RotateRight32(d ^ a, 8);
        c = unchecked(c + d);
        b = RotateRight32(b ^ c, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateRight32(uint x, int r) => BitOperations.RotateRight(x, r);
}