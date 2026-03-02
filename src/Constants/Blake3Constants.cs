namespace Soenneker.Hashing.Blake3.Constants;

internal static class Blake3Constants
{
    public const int OutLen = 32;

    public static readonly uint[] Iv =
    [
        0x6a09e667u, 0xbb67ae85u, 0x3c6ef372u, 0xa54ff53au,
        0x510e527fu, 0x9b05688cu, 0x1f83d9abu, 0x5be0cd19u
    ];

    public const int BlockLen = 64;
    public const int ChunkLen = 1024;
    public const int Rounds = 7;

    // BLAKE3 permutation P
    private static readonly byte[] _p = [2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8];

    // Precompute per-round message indices (no per-round permute temps)
    public static readonly byte[][] MIndex;

    static Blake3Constants()
    {
        MIndex = new byte[Rounds][];
        var idx0 = new byte[16];
        for (byte i = 0; i < 16; i++) idx0[i] = i;
        MIndex[0] = (byte[])idx0.Clone();

        for (var r = 1; r < Rounds; r++)
        {
            byte[] prev = MIndex[r - 1];
            var cur = new byte[16];
            for (var i = 0; i < 16; i++)
                cur[i] = prev[_p[i]];
            MIndex[r] = cur;
        }
    }
}