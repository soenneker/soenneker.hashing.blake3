namespace Soenneker.Hashing.Blake3.Tests;

/// <summary>
/// Builds the official BLAKE3 test vector input: first <paramref name="length"/> bytes
/// of the repeating sequence 0, 1, 2, ..., 250, 0, 1, ...
/// </summary>
internal static class Blake3TestVectors
{
    public static byte[] GetTestInput(int length)
    {
        var buf = new byte[length];
        for (int i = 0; i < length; i++)
            buf[i] = (byte)(i % 251);
        return buf;
    }
}
