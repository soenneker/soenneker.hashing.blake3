using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Arrays.Bytes;
using Soenneker.Hashing.Blake3.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.MemoryStream.Abstract;

namespace Soenneker.Hashing.Blake3;

/// <inheritdoc cref="IBlake3Util"/>
public sealed class Blake3Util : IBlake3Util
{
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IMemoryStreamUtil _memoryStreamUtil;

    public Blake3Util(IFileUtil fileUtil, IDirectoryUtil directoryUtil, IMemoryStreamUtil memoryStreamUtil)
    {
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _memoryStreamUtil = memoryStreamUtil;
    }

    public async ValueTask<string> HashFile(string path, CancellationToken cancellationToken = default)
    {
        byte[] hash = await HashFileToByteArray(path, cancellationToken)
            .NoSync();

        return hash.ToHexLower();
    }

    public async ValueTask<byte[]> HashFileToByteArray(string path, CancellationToken cancellationToken = default)
    {
        if (path.IsNullOrWhiteSpace())
            throw new ArgumentNullException(nameof(path));

        var result = new byte[32];
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        try
        {
            await HashFile(path, result, buffer, cancellationToken).NoSync();
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask<Dictionary<string, byte[]>> HashDirectory(string path, CancellationToken cancellationToken = default)
    {
        if (path.IsNullOrWhiteSpace())
            throw new ArgumentNullException(nameof(path));

        bool exists = await _directoryUtil.Exists(path, cancellationToken)
                                          .NoSync();
        if (!exists)
            throw new DirectoryNotFoundException($"The directory does not exist: {path}.");

        List<string> files = await _directoryUtil.GetFilesByExtension(path, "", true, cancellationToken)
                                                 .NoSync();

        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                byte[] hash = await HashFileToByteArray(filePath, cancellationToken)
                    .NoSync();
                result[filePath] = hash;
            }
            catch (Exception)
            {
                // Skip files that cannot be read (e.g. access denied)
            }
        }

        return result;
    }

    public async ValueTask<string> HashDirectoryToAggregateString(string path, CancellationToken cancellationToken = default)
    {
        if (path.IsNullOrWhiteSpace())
            throw new ArgumentNullException(nameof(path));

        bool exists = await _directoryUtil.Exists(path, cancellationToken)
                                          .NoSync();
        if (!exists)
            throw new DirectoryNotFoundException($"The directory does not exist: {path}.");

        List<string> files = await _directoryUtil.GetFilesByExtension(path, "", true, cancellationToken)
                                                 .NoSync();

        files.Sort(StringComparer.Ordinal);

        if (files.Count == 0)
            return string.Empty;

        using var aggregateHasher = new Blake3Hasher.Incremental();
        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        var fileHash = new byte[32];

        try
        {
            foreach (string filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await HashFile(filePath, fileHash, readBuffer, cancellationToken).NoSync();
                    AppendUtf8(aggregateHasher, Path.GetRelativePath(path, filePath));
                    aggregateHasher.Append(fileHash);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Skip files that cannot be read (e.g. access denied)
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }

        byte[] aggregateHash = aggregateHasher.FinalizeHash();
        return aggregateHash.ToHexLower();
    }

    private async ValueTask HashFile(string path, Memory<byte> destination, byte[] buffer, CancellationToken cancellationToken)
    {
        await using FileStream stream = _fileUtil.OpenRead(path, log: false);
        using var hasher = new Blake3Hasher.Incremental();

        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).NoSync();
            if (read == 0)
                break;

            hasher.Append(buffer.AsSpan(0, read));
        }

        hasher.FinalizeHash(destination.Span);
    }

    private static void AppendUtf8(Blake3Hasher.Incremental hasher, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        byte[]? rented = null;
        Span<byte> bytes = byteCount <= 512
            ? stackalloc byte[byteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount)).AsSpan(0, byteCount);

        try
        {
            Encoding.UTF8.GetBytes(value, bytes);
            hasher.Append(bytes);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
