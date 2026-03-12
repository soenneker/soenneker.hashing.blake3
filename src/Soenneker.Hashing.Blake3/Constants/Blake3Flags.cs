namespace Soenneker.Hashing.Blake3.Constants;

/// <summary>
/// BLAKE3 compression block flags (matches C/Rust reference).
/// </summary>
internal static class Blake3Flags
{
    public const uint ChunkStart = 1u << 0;
    public const uint ChunkEnd = 1u << 1;
    public const uint Parent = 1u << 2;
    public const uint Root = 1u << 3;
}
