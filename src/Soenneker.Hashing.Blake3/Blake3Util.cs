using System;
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

        byte[] content = await _fileUtil.ReadToBytes(path, false, cancellationToken)
                                        .NoSync();

        return Blake3Hasher.Hash(content);
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

        using MemoryStream aggregateStream = await _memoryStreamUtil.Get(cancellationToken)
                                                                    .NoSync();

        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                byte[] fileHash = await HashFileToByteArray(filePath, cancellationToken)
                    .NoSync();
                string relativePath = Path.GetRelativePath(path, filePath);
                byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);

                aggregateStream.Write(pathBytes, 0, pathBytes.Length);
                aggregateStream.Write(fileHash, 0, fileHash.Length);
            }
            catch (Exception)
            {
                // Skip files that cannot be read (e.g. access denied)
            }
        }

        byte[] aggregateInput = aggregateStream.ToArray();
        byte[] aggregateHash = Blake3Hasher.Hash(aggregateInput);
        return aggregateHash.ToHexLower();
    }
}