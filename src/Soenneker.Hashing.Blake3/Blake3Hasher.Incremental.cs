using Soenneker.Hashing.Blake3.ChunkState;
using Soenneker.Hashing.Blake3.Compress;
using Soenneker.Hashing.Blake3.Constants;
using System;
using System.Buffers;
namespace Soenneker.Hashing.Blake3;
public static partial class Blake3Hasher
{
    /// <summary>Incrementally computes a BLAKE3 digest without retaining the complete input.</summary>
    public sealed class Incremental : IDisposable
    {
        private byte[] _chunk = ArrayPool<byte>.Shared.Rent(Blake3Constants.ChunkLen);
        private uint[] _cvStack = ArrayPool<uint>.Shared.Rent(64 * 8);
        private int _chunkLength;
        private int _cvStackLength;
        private ulong _completedChunks;
        private bool _finalized;
        private bool _disposed;
        /// <summary>
        /// Finalizes hash for the incremental.
        /// </summary>
        /// <param name="input">input to read or transform.</param>
        public void Append(ReadOnlySpan<byte> input)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_finalized)
                throw new InvalidOperationException("The hash has already been finalized.");

            while (!input.IsEmpty)
            {
                // Keep the final full chunk buffered until more input proves it is not the root chunk.
                if (_chunkLength == Blake3Constants.ChunkLen)
                    CompleteBufferedChunk();

                int take = Math.Min(Blake3Constants.ChunkLen - _chunkLength, input.Length);
                input[..take].CopyTo(_chunk.AsSpan(_chunkLength));
                _chunkLength += take;
                input = input[take..];
            }
        }

        /// <summary>
        /// Finalizes hash for the Incremental.
        /// </summary>
        /// <returns>The resulting byte[].</returns>
        public byte[] FinalizeHash()
        {
            var result = new byte[Blake3Constants.OutLen];
            FinalizeHash(result);
            return result;
        }

        /// <summary>
        /// Finalizes hash for the Incremental.
        /// </summary>
        /// <param name="destination">destination that receives the result.</param>
        public void FinalizeHash(Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_finalized)
                throw new InvalidOperationException("The hash has already been finalized.");
            if (destination.Length < Blake3Constants.OutLen)
                throw new ArgumentException($"Destination must be at least {Blake3Constants.OutLen} bytes.", nameof(destination));

            _finalized = true;

            if (_completedChunks == 0)
            {
                Blake3ChunkState.ChunkRoot32(_chunk.AsSpan(0, _chunkLength), 0, 0, destination);
                return;
            }

            Span<uint> current = stackalloc uint[8];
            Span<uint> left = stackalloc uint[8];
            Span<uint> parentBlock = stackalloc uint[16];
            Blake3ChunkState.ChunkToCv(_chunk.AsSpan(0, _chunkLength), _completedChunks, 0, current);

            int stackIndex = _cvStackLength;
            while (stackIndex > 0)
            {
                stackIndex--;
                _cvStack.AsSpan(stackIndex * 8, 8).CopyTo(left);
                left.CopyTo(parentBlock[..8]);
                current.CopyTo(parentBlock[8..]);

                if (stackIndex == 0)
                {
                    Blake3Compress.CompressRoot32(Blake3Constants.Iv, parentBlock, 0, Blake3Constants.BlockLen,
                        Blake3Flags.Parent | Blake3Flags.Root, destination);
                    return;
                }

                Blake3Compress.CompressCv(Blake3Constants.Iv, parentBlock, 0, Blake3Constants.BlockLen, Blake3Flags.Parent, current);
            }
        }

        private void CompleteBufferedChunk()
        {
            Span<uint> cv = stackalloc uint[8];
            Span<uint> parentBlock = stackalloc uint[16];
            Span<uint> left = stackalloc uint[8];
            Span<uint> right = stackalloc uint[8];

            Blake3ChunkState.ChunkToCv(_chunk.AsSpan(0, Blake3Constants.ChunkLen), _completedChunks, 0, cv);
            _completedChunks++;
            AddChunkCv(_cvStack, ref _cvStackLength, cv, _completedChunks, parentBlock, left, right);
            _chunkLength = 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ArrayPool<byte>.Shared.Return(_chunk);
            ArrayPool<uint>.Shared.Return(_cvStack);
            _chunk = [];
            _cvStack = [];
            _disposed = true;
        }
    }
}
