using Soenneker.Hashing.Blake3.Constants;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace Soenneker.Hashing.Blake3.Simd;

/// <summary>
/// NEON 4-lane BLAKE3 chunk compression. Hashes 4 full chunks in parallel.
/// </summary>
internal static class Blake3Neon
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ChunkToCvBatch(ReadOnlySpan<byte> input, int startChunk, Span<uint> destWords)
    {
        Vector128<uint> v0 = V128(Blake3Constants.Iv[0]);
        Vector128<uint> v1 = V128(Blake3Constants.Iv[1]);
        Vector128<uint> v2 = V128(Blake3Constants.Iv[2]);
        Vector128<uint> v3 = V128(Blake3Constants.Iv[3]);
        Vector128<uint> v4 = V128(Blake3Constants.Iv[4]);
        Vector128<uint> v5 = V128(Blake3Constants.Iv[5]);
        Vector128<uint> v6 = V128(Blake3Constants.Iv[6]);
        Vector128<uint> v7 = V128(Blake3Constants.Iv[7]);

        Vector128<uint> v8 = V128(Blake3Constants.Iv[0]);
        Vector128<uint> v9 = V128(Blake3Constants.Iv[1]);
        Vector128<uint> v10 = V128(Blake3Constants.Iv[2]);
        Vector128<uint> v11 = V128(Blake3Constants.Iv[3]);

        var ctrLo = Vector128.Create((uint)(startChunk + 0), (uint)(startChunk + 1), (uint)(startChunk + 2), (uint)(startChunk + 3));
        Vector128<uint> ctrHi = Vector128<uint>.Zero;

        int baseOff = startChunk * Blake3Constants.ChunkLen;
        Span<Vector128<uint>> m = stackalloc Vector128<uint>[16];

        for (var block = 0; block < 16; block++)
        {
            int blockOff = baseOff + block * Blake3Constants.BlockLen;

            for (var w = 0; w < 16; w++)
                m[w] = LoadU32X4(input, blockOff + w * 4);

            Vector128<uint> vv0 = v0, vv1 = v1, vv2 = v2, vv3 = v3, vv4 = v4, vv5 = v5, vv6 = v6, vv7 = v7;
            Vector128<uint> vv8 = v8, vv9 = v9, vv10 = v10, vv11 = v11;

            Vector128<uint> vv12 = ctrLo;
            Vector128<uint> vv13 = ctrHi;
            Vector128<uint> vv14 = V128(Blake3Constants.BlockLen);

            uint fl = (block == 0 ? Blake3Flags.ChunkStart : 0) | (block == 15 ? Blake3Flags.ChunkEnd : 0);
            Vector128<uint> vv15 = V128(fl);

            for (var r = 0; r < Blake3Constants.Rounds; r++)
            {
                byte[] s = Blake3Constants.MIndex[r];

                G(ref vv0, ref vv4, ref vv8, ref vv12, m[s[0]], m[s[1]]);
                G(ref vv1, ref vv5, ref vv9, ref vv13, m[s[2]], m[s[3]]);
                G(ref vv2, ref vv6, ref vv10, ref vv14, m[s[4]], m[s[5]]);
                G(ref vv3, ref vv7, ref vv11, ref vv15, m[s[6]], m[s[7]]);

                G(ref vv0, ref vv5, ref vv10, ref vv15, m[s[8]], m[s[9]]);
                G(ref vv1, ref vv6, ref vv11, ref vv12, m[s[10]], m[s[11]]);
                G(ref vv2, ref vv7, ref vv8, ref vv13, m[s[12]], m[s[13]]);
                G(ref vv3, ref vv4, ref vv9, ref vv14, m[s[14]], m[s[15]]);
            }

            v0 = AdvSimd.Xor(vv0, vv8);
            v1 = AdvSimd.Xor(vv1, vv9);
            v2 = AdvSimd.Xor(vv2, vv10);
            v3 = AdvSimd.Xor(vv3, vv11);
            v4 = AdvSimd.Xor(vv4, vv12);
            v5 = AdvSimd.Xor(vv5, vv13);
            v6 = AdvSimd.Xor(vv6, vv14);
            v7 = AdvSimd.Xor(vv7, vv15);
        }

        StoreCv4(v0, v1, v2, v3, v4, v5, v6, v7, destWords);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> V128(uint x) => Vector128.Create(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> LoadU32X4(ReadOnlySpan<byte> input, int wordOff)
    {
        uint w0 = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(wordOff + 0 * Blake3Constants.ChunkLen, 4));
        uint w1 = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(wordOff + 1 * Blake3Constants.ChunkLen, 4));
        uint w2 = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(wordOff + 2 * Blake3Constants.ChunkLen, 4));
        uint w3 = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(wordOff + 3 * Blake3Constants.ChunkLen, 4));
        return Vector128.Create(w0, w1, w2, w3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void G(ref Vector128<uint> a, ref Vector128<uint> b, ref Vector128<uint> c, ref Vector128<uint> d, Vector128<uint> mx, Vector128<uint> my)
    {
        a = AdvSimd.Add(a, AdvSimd.Add(b, mx));
        d = RotateRight16(AdvSimd.Xor(d, a));
        c = AdvSimd.Add(c, d);
        b = RotateRight12(AdvSimd.Xor(b, c));
        a = AdvSimd.Add(a, AdvSimd.Add(b, my));
        d = RotateRight8(AdvSimd.Xor(d, a));
        c = AdvSimd.Add(c, d);
        b = RotateRight7(AdvSimd.Xor(b, c));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight16(Vector128<uint> x)
    {
        Vector128<uint> sr = AdvSimd.ShiftRightLogical(x, 16);
        Vector128<uint> sl = AdvSimd.ShiftLeftLogical(x, 16);
        return AdvSimd.Or(sr, sl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight12(Vector128<uint> x)
    {
        Vector128<uint> sr = AdvSimd.ShiftRightLogical(x, 12);
        Vector128<uint> sl = AdvSimd.ShiftLeftLogical(x, 20);
        return AdvSimd.Or(sr, sl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight8(Vector128<uint> x)
    {
        Vector128<uint> sr = AdvSimd.ShiftRightLogical(x, 8);
        Vector128<uint> sl = AdvSimd.ShiftLeftLogical(x, 24);
        return AdvSimd.Or(sr, sl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight7(Vector128<uint> x)
    {
        Vector128<uint> sr = AdvSimd.ShiftRightLogical(x, 7);
        Vector128<uint> sl = AdvSimd.ShiftLeftLogical(x, 25);
        return AdvSimd.Or(sr, sl);
    }

    private static void StoreCv4(
        Vector128<uint> v0, Vector128<uint> v1, Vector128<uint> v2, Vector128<uint> v3,
        Vector128<uint> v4, Vector128<uint> v5, Vector128<uint> v6, Vector128<uint> v7,
        Span<uint> dest)
    {
        for (var lane = 0; lane < 4; lane++)
        {
            int offset = lane * 8;
            dest[offset + 0] = v0.GetElement(lane);
            dest[offset + 1] = v1.GetElement(lane);
            dest[offset + 2] = v2.GetElement(lane);
            dest[offset + 3] = v3.GetElement(lane);
            dest[offset + 4] = v4.GetElement(lane);
            dest[offset + 5] = v5.GetElement(lane);
            dest[offset + 6] = v6.GetElement(lane);
            dest[offset + 7] = v7.GetElement(lane);
        }
    }
}
