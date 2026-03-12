using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Hashing.Blake3.Abstract;

/// <summary>
/// Utility for BLAKE3 hashing of files and directories.
/// </summary>
public interface IBlake3Util
{
    /// <summary>
    /// Computes the BLAKE3 hash of the file at the given path and returns the digest as a lowercase hex string.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A 64-character lowercase hex string of the 32-byte BLAKE3 digest.</returns>
    [Pure]
    ValueTask<string> HashFile(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the BLAKE3 hash of the file at the given path and returns the 32-byte digest.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A 32-byte array containing the BLAKE3 hash of the file contents.</returns>
    [Pure]
    ValueTask<byte[]> HashFileToByteArray(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the BLAKE3 hash of each file in the directory (and optionally subdirectories) and returns a map of file path to digest bytes.
    /// </summary>
    /// <param name="path">The full path to the directory.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dictionary of file path to 32-byte BLAKE3 digest.</returns>
    [Pure]
    ValueTask<Dictionary<string, byte[]>> HashDirectory(string path, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a single aggregate BLAKE3 hash of the directory by hashing each file (relative path + file hash) in sorted order and hashing the combined input. Returns the digest as a lowercase hex string.
    /// </summary>
    /// <param name="path">The full path to the directory.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A 64-character lowercase hex string of the aggregate BLAKE3 digest.</returns>
    [Pure]
    ValueTask<string> HashDirectoryToAggregateString(string path,
        CancellationToken cancellationToken = default);
}