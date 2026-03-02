using Soenneker.Hashing.Blake3.Constants;
using Soenneker.Hashing.Blake3.Compress;
using System;

namespace Soenneker.Hashing.Blake3.Parent;

/// <summary>
/// Parent node compression: combine two child CVs into one parent CV. Matches parent_output in C.
/// </summary>
internal static class Blake3Parent
{
    public static void ParentCv(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, ReadOnlySpan<uint> keyWords, uint flags, Span<uint> destCv8)
    {
        if (left.Length < 8 || right.Length < 8 || keyWords.Length < 8 || destCv8.Length < 8)
            throw new ArgumentException("ParentCv requires 8-word left/right/key and destination spans.");

        Span<uint> m = stackalloc uint[16];
        for (var i = 0; i < 8; i++)
            m[i] = left[i];
        for (var i = 0; i < 8; i++)
            m[8 + i] = right[i];

        Blake3Compress.CompressCv(keyWords, m, 0, Blake3Constants.BlockLen, flags | Blake3Flags.Parent, destCv8);
    }

    public static void ParentRoot32(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, ReadOnlySpan<uint> keyWords, uint flags, Span<byte> destination32)
    {
        if (left.Length < 8 || right.Length < 8 || keyWords.Length < 8)
            throw new ArgumentException("ParentRoot32 requires 8-word left/right/key spans.");

        Span<uint> m = stackalloc uint[16];
        for (var i = 0; i < 8; i++)
            m[i] = left[i];
        for (var i = 0; i < 8; i++)
            m[8 + i] = right[i];

        Blake3Compress.CompressRoot32(keyWords, m, 0, Blake3Constants.BlockLen, flags | Blake3Flags.Parent | Blake3Flags.Root, destination32);
    }
}
