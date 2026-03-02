using Soenneker.Hashing.Blake3.Constants;
using System;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;

namespace Soenneker.Hashing.Blake3;

/// <summary>
/// BLAKE3 verification: compare hash of input to an expected digest (constant-time).
/// </summary>
public static partial class Blake3Util
{
    /// <summary>
    /// Returns true if the BLAKE3 hash of <paramref name="input"/> equals <paramref name="expectedHash"/> (constant-time comparison).
    /// </summary>
    /// <param name="input">Data to hash.</param>
    /// <param name="expectedHash">Expected 32-byte BLAKE3 digest.</param>
    /// <returns><c>true</c> if the computed hash matches <paramref name="expectedHash"/> and it is 32 bytes; otherwise <c>false</c>.</returns>
    [Pure]
    public static bool Verify(ReadOnlySpan<byte> input, ReadOnlySpan<byte> expectedHash)
    {
        if (expectedHash.Length != Blake3Constants.OutLen)
            return false;

        Span<byte> computed = stackalloc byte[Blake3Constants.OutLen];
        Hash(input, computed);
        return CryptographicOperations.FixedTimeEquals(computed, expectedHash);
    }

    /// <summary>
    /// Returns true if the BLAKE3 hash of <paramref name="input"/> equals <paramref name="expectedHash"/> (constant-time comparison).
    /// </summary>
    /// <param name="input">Data to hash.</param>
    /// <param name="expectedHash">Expected 32-byte BLAKE3 digest.</param>
    /// <returns><c>true</c> if the computed hash matches <paramref name="expectedHash"/> and it is 32 bytes; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="expectedHash"/> is <c>null</c>.</exception>
    [Pure]
    public static bool Verify(byte[] input, byte[] expectedHash)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        if (expectedHash is null)
            throw new ArgumentNullException(nameof(expectedHash));

        return Verify((ReadOnlySpan<byte>)input, expectedHash);
    }

    /// <summary>
    /// Returns true if the BLAKE3 hash of <paramref name="input"/> (UTF-8) equals <paramref name="expectedHash"/> (constant-time comparison).
    /// </summary>
    /// <param name="input">The string to hash (encoded as UTF-8).</param>
    /// <param name="expectedHash">Expected 32-byte BLAKE3 digest.</param>
    /// <returns><c>true</c> if the computed hash matches <paramref name="expectedHash"/> and it is 32 bytes; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
    [Pure]
    public static bool Verify(string input, ReadOnlySpan<byte> expectedHash)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        if (expectedHash.Length != Blake3Constants.OutLen)
            return false;

        byte[] computed = Hash(input);

        try
        {
            return CryptographicOperations.FixedTimeEquals(computed, expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computed);
        }
    }

    /// <summary>
    /// Returns true if the BLAKE3 hash of <paramref name="input"/> (UTF-8) equals the digest given as hex string <paramref name="expectedHashHex"/> (constant-time comparison).
    /// </summary>
    /// <param name="input">The string to hash (encoded as UTF-8).</param>
    /// <param name="expectedHashHex">Expected 64-character hex string (lowercase or uppercase).</param>
    /// <returns><c>true</c> if the computed hash matches the decoded <paramref name="expectedHashHex"/> and it decodes to 32 bytes; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="expectedHashHex"/> is <c>null</c>.</exception>
    [Pure]
    public static bool Verify(string input, string expectedHashHex)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));
        if (expectedHashHex is null)
            throw new ArgumentNullException(nameof(expectedHashHex));

        byte[] expected = Convert.FromHexString(expectedHashHex);
        if (expected.Length != Blake3Constants.OutLen)
            return false;

        try
        {
            return Verify(input, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
        }
    }
}