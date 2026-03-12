using Soenneker.Hashing.Blake3.Constants;
using Soenneker.Hashing.Blake3.Simd;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Soenneker.Hashing.Blake3.Compress;

internal static class Blake3CompressSse41
{
    public static bool IsSupported => Sse2.IsSupported && Ssse3.IsSupported;

    private static readonly Vector128<byte> _rot16Mask = Vector128.Create((byte)2, 3, 0, 1, 6, 7, 4, 5, 10, 11, 8, 9, 14, 15, 12, 13);

    private static readonly Vector128<byte> _rot8Mask = Vector128.Create((byte)1, 2, 3, 0, 5, 6, 7, 4, 9, 10, 11, 8, 13, 14, 15, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight16(Vector128<uint> v)
    {
        if (Avx512F.VL.IsSupported)
            return Avx512F.VL.RotateRight(v, 16);

        return Ssse3.Shuffle(v.AsByte(), _rot16Mask)
                    .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight12(Vector128<uint> v)
    {
        if (Avx512F.VL.IsSupported)
            return Avx512F.VL.RotateRight(v, 12);

        return Sse2.Or(Sse2.ShiftRightLogical(v, 12), Sse2.ShiftLeftLogical(v, 20));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight8(Vector128<uint> v)
    {
        if (Avx512F.VL.IsSupported)
            return Avx512F.VL.RotateRight(v, 8);

        return Ssse3.Shuffle(v.AsByte(), _rot8Mask)
                    .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> RotateRight7(Vector128<uint> v)
    {
        if (Avx512F.VL.IsSupported)
            return Avx512F.VL.RotateRight(v, 7);

        return Sse2.Or(Sse2.ShiftRightLogical(v, 7), Sse2.ShiftLeftLogical(v, 25));
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Compress(ReadOnlySpan<uint> cv, ReadOnlySpan<uint> block, ulong counter, uint blockLen, uint flags, Span<uint> output)
    {
        ref uint cvRef = ref MemoryMarshal.GetReference(cv);
        Vector128<uint> row0 = VectorCompat.Load(ref cvRef);
        Vector128<uint> row1 = VectorCompat.Load(ref cvRef, 4);

        ref uint ivRef = ref MemoryMarshal.GetReference(Blake3Constants.Iv);
        Vector128<uint> row2 = VectorCompat.Load(ref ivRef);
        Vector128<uint> row3 = Vector128.Create((uint)counter, (uint)(counter >> 32), blockLen, flags);

        ref uint mRef = ref MemoryMarshal.GetReference(block);
        Vector128<uint> m0 = VectorCompat.Load(ref mRef);
        Vector128<uint> m1 = VectorCompat.Load(ref mRef, 4);
        Vector128<uint> m2 = VectorCompat.Load(ref mRef, 8);
        Vector128<uint> m3 = VectorCompat.Load(ref mRef, 12);

        DoRoundsShuffle(ref row0, ref row1, ref row2, ref row3, m0, m1, m2, m3);

        Vector128<uint> cv0 = VectorCompat.Load(ref cvRef);
        Vector128<uint> cv1 = VectorCompat.Load(ref cvRef, 4);

        ref uint outRef = ref MemoryMarshal.GetReference(output);
        VectorCompat.Store(Sse2.Xor(row0, row2), ref outRef);
        VectorCompat.Store(Sse2.Xor(row1, row3), ref outRef, 4);
        VectorCompat.Store(Sse2.Xor(row2, cv0), ref outRef, 8);
        VectorCompat.Store(Sse2.Xor(row3, cv1), ref outRef, 12);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void CompressChainingValue(ReadOnlySpan<uint> cv, ReadOnlySpan<uint> block, ulong counter, uint blockLen, uint flags,
        Span<uint> chainingValue)
    {
        ref uint cvRef = ref MemoryMarshal.GetReference(cv);
        Vector128<uint> row0 = VectorCompat.Load(ref cvRef);
        Vector128<uint> row1 = VectorCompat.Load(ref cvRef, 4);

        ref uint ivRef = ref MemoryMarshal.GetReference(Blake3Constants.Iv);
        Vector128<uint> row2 = VectorCompat.Load(ref ivRef);
        Vector128<uint> row3 = Vector128.Create((uint)counter, (uint)(counter >> 32), blockLen, flags);

        ref uint mRef = ref MemoryMarshal.GetReference(block);
        Vector128<uint> m0 = VectorCompat.Load(ref mRef);
        Vector128<uint> m1 = VectorCompat.Load(ref mRef, 4);
        Vector128<uint> m2 = VectorCompat.Load(ref mRef, 8);
        Vector128<uint> m3 = VectorCompat.Load(ref mRef, 12);

        DoRoundsShuffle(ref row0, ref row1, ref row2, ref row3, m0, m1, m2, m3);

        ref uint outRef = ref MemoryMarshal.GetReference(chainingValue);
        VectorCompat.Store(Sse2.Xor(row0, row2), ref outRef);
        VectorCompat.Store(Sse2.Xor(row1, row3), ref outRef, 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void DoRoundsShuffle(ref Vector128<uint> row0_ref, ref Vector128<uint> row1_ref, ref Vector128<uint> row2_ref, ref Vector128<uint> row3_ref,
        Vector128<uint> m0, Vector128<uint> m1, Vector128<uint> m2, Vector128<uint> m3)
    {
        Vector128<uint> row0 = row0_ref;
        Vector128<uint> row1 = row1_ref;
        Vector128<uint> row2 = row2_ref;
        Vector128<uint> row3 = row3_ref;
        Vector128<uint> b0, t0, t1, p0, p1, p2;

        p0 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_10_00_10_00)
                .AsUInt32();
        b0 = p0;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        p1 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_11_01_11_01)
                .AsUInt32();
        b0 = p1;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_10_01_00_11);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_00_11_10_01);

        t0 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_10_00_10_00)
                .AsUInt32();
        p2 = Sse2.Shuffle(t0, 0b_10_01_00_11);
        b0 = p2;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_11_01_11_01)
                .AsUInt32();
        m3 = Sse2.Shuffle(t0, 0b_10_01_00_11);
        m0 = p0;
        m1 = p1;
        m2 = p2;
        b0 = m3;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_00_11_10_01);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_10_01_00_11);

        // ===== ROUND 2 =====
        t0 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_11_01_10_01)
                .AsUInt32();
        p0 = Sse2.Shuffle(t0, 0b_01_11_10_00);
        b0 = p0;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.UnpackHigh(m0, m2);
        t1 = Sse2.Shuffle(m0.AsDouble(), m3.AsDouble(), 0b_10)
                 .AsUInt32();
        p1 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_11_00_01_10)
                .AsUInt32();
        b0 = p1;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_10_01_00_11);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_00_11_10_01);

        t0 = Sse2.UnpackLow(m3, m1);
        t1 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_10_01_11_01)
                .AsUInt32();
        p2 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_10_01_01_00)
                .AsUInt32();
        b0 = p2;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.Shuffle(m2.AsDouble(), m1.AsDouble(), 0b_10)
                 .AsUInt32();
        m3 = Sse.Shuffle(t1.AsSingle(), t0.AsSingle(), 0b_00_10_11_00)
                .AsUInt32();
        m0 = p0;
        m1 = p1;
        m2 = p2;
        b0 = m3;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_00_11_10_01);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_10_01_00_11);

        // ===== ROUND 3 =====
        t0 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_11_01_10_01)
                .AsUInt32();
        p0 = Sse2.Shuffle(t0, 0b_01_11_10_00);
        b0 = p0;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.UnpackHigh(m0, m2);
        t1 = Sse2.Shuffle(m0.AsDouble(), m3.AsDouble(), 0b_10)
                 .AsUInt32();
        p1 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_11_00_01_10)
                .AsUInt32();
        b0 = p1;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_10_01_00_11);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_00_11_10_01);

        t0 = Sse2.UnpackLow(m3, m1);
        t1 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_10_01_11_01)
                .AsUInt32();
        p2 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_10_01_01_00)
                .AsUInt32();
        b0 = p2;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.Shuffle(m2.AsDouble(), m1.AsDouble(), 0b_10)
                 .AsUInt32();
        m3 = Sse.Shuffle(t1.AsSingle(), t0.AsSingle(), 0b_00_10_11_00)
                .AsUInt32();
        m0 = p0;
        m1 = p1;
        m2 = p2;
        b0 = m3;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_00_11_10_01);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_10_01_00_11);

        t0 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_11_01_10_01)
                .AsUInt32();
        p0 = Sse2.Shuffle(t0, 0b_01_11_10_00);
        b0 = p0;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.UnpackHigh(m0, m2);
        t1 = Sse2.Shuffle(m0.AsDouble(), m3.AsDouble(), 0b_10)
                 .AsUInt32();
        p1 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_11_00_01_10)
                .AsUInt32();
        b0 = p1;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_10_01_00_11);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_00_11_10_01);

        t0 = Sse2.UnpackLow(m3, m1);
        t1 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_10_01_11_01)
                .AsUInt32();
        p2 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_10_01_01_00)
                .AsUInt32();
        b0 = p2;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.Shuffle(m2.AsDouble(), m1.AsDouble(), 0b_10)
                 .AsUInt32();
        m3 = Sse.Shuffle(t1.AsSingle(), t0.AsSingle(), 0b_00_10_11_00)
                .AsUInt32();
        m0 = p0;
        m1 = p1;
        m2 = p2;
        b0 = m3;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_00_11_10_01);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_10_01_00_11);

        t0 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_11_01_10_01)
                .AsUInt32();
        p0 = Sse2.Shuffle(t0, 0b_01_11_10_00);
        b0 = p0;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.UnpackHigh(m0, m2);
        t1 = Sse2.Shuffle(m0.AsDouble(), m3.AsDouble(), 0b_10)
                 .AsUInt32();
        p1 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_11_00_01_10)
                .AsUInt32();
        b0 = p1;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_10_01_00_11);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_00_11_10_01);

        t0 = Sse2.UnpackLow(m3, m1);
        t1 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_10_01_11_01)
                .AsUInt32();
        p2 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_10_01_01_00)
                .AsUInt32();
        b0 = p2;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.Shuffle(m2.AsDouble(), m1.AsDouble(), 0b_10)
                 .AsUInt32();
        m3 = Sse.Shuffle(t1.AsSingle(), t0.AsSingle(), 0b_00_10_11_00)
                .AsUInt32();
        m0 = p0;
        m1 = p1;
        m2 = p2;
        b0 = m3;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_00_11_10_01);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_10_01_00_11);

        t0 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_11_01_10_01)
                .AsUInt32();
        p0 = Sse2.Shuffle(t0, 0b_01_11_10_00);
        b0 = p0;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.UnpackHigh(m0, m2);
        t1 = Sse2.Shuffle(m0.AsDouble(), m3.AsDouble(), 0b_10)
                 .AsUInt32();
        p1 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_11_00_01_10)
                .AsUInt32();
        b0 = p1;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_10_01_00_11);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_00_11_10_01);

        t0 = Sse2.UnpackLow(m3, m1);
        t1 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_10_01_11_01)
                .AsUInt32();
        p2 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_10_01_01_00)
                .AsUInt32();
        b0 = p2;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.Shuffle(m2.AsDouble(), m1.AsDouble(), 0b_10)
                 .AsUInt32();
        m3 = Sse.Shuffle(t1.AsSingle(), t0.AsSingle(), 0b_00_10_11_00)
                .AsUInt32();
        m0 = p0;
        m1 = p1;
        m2 = p2;
        b0 = m3;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_00_11_10_01);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_10_01_00_11);

        t0 = Sse.Shuffle(m0.AsSingle(), m1.AsSingle(), 0b_11_01_10_01)
                .AsUInt32();
        p0 = Sse2.Shuffle(t0, 0b_01_11_10_00);
        b0 = p0;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.UnpackHigh(m0, m2);
        t1 = Sse2.Shuffle(m0.AsDouble(), m3.AsDouble(), 0b_10)
                 .AsUInt32();
        p1 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_11_00_01_10)
                .AsUInt32();
        b0 = p1;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_10_01_00_11);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_00_11_10_01);

        t0 = Sse2.UnpackLow(m3, m1);
        t1 = Sse.Shuffle(m2.AsSingle(), m3.AsSingle(), 0b_10_01_11_01)
                .AsUInt32();
        p2 = Sse.Shuffle(t0.AsSingle(), t1.AsSingle(), 0b_10_01_01_00)
                .AsUInt32();
        b0 = p2;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight16(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight12(Sse2.Xor(row1, row2));

        t0 = Sse2.Shuffle(m2.AsDouble(), m1.AsDouble(), 0b_10)
                 .AsUInt32();
        m3 = Sse.Shuffle(t1.AsSingle(), t0.AsSingle(), 0b_00_10_11_00)
                .AsUInt32();
        b0 = m3;
        row0 = Sse2.Add(Sse2.Add(row0, b0), row1);
        row3 = RotateRight8(Sse2.Xor(row3, row0));
        row2 = Sse2.Add(row2, row3);
        row1 = RotateRight7(Sse2.Xor(row1, row2));

        row0 = Sse2.Shuffle(row0, 0b_00_11_10_01);
        row3 = Sse2.Shuffle(row3, 0b_01_00_11_10);
        row2 = Sse2.Shuffle(row2, 0b_10_01_00_11);

        row0_ref = row0;
        row1_ref = row1;
        row2_ref = row2;
        row3_ref = row3;
    }
}