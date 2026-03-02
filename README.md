[![](https://img.shields.io/nuget/v/soenneker.hashing.blake3.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.hashing.blake3/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.hashing.blake3/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.hashing.blake3/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.hashing.blake3.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.hashing.blake3/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Hashing.Blake3

A high-performance .NET library for **BLAKE3** hashing and constant-time verification. Pure C# with optional SIMD acceleration (AVX2 on x64, NEON on ARM).

## Features

- **BLAKE3** — 256-bit cryptographic hash; fast, secure, and specified in [the official BLAKE3 spec](https://github.com/BLAKE3-team/BLAKE3-specs).
- **Zero dependencies** — only the .NET runtime; no native or third-party packages.
- **SIMD** — uses AVX2 (x64) and NEON (ARM) when available for faster chunk hashing.
- **Parallel hashing** — `HashParallel` for large inputs; multi-threaded with batched SIMD where applicable.
- **Span/Memory-friendly** — hashing from `byte[]`, `ReadOnlySpan<byte>`, `ReadOnlyMemory<byte>`, and in-place digest writing.
- **Strings** — hash UTF-8 strings and get hex output via `Hash(string)` and `HashToString(string)`.
- **Constant-time verification** — `Verify` uses `CryptographicOperations.FixedTimeEquals` for digest comparison.
- **Blake3Util** — optional DI-registered utility for **file and directory** hashing (hash single files, whole directories, or an aggregate hash of a directory).

## Installation

```bash
dotnet add package Soenneker.Hashing.Blake3
```

## Quick Start

```csharp
using Soenneker.Hashing.Blake3;

// Hash bytes → 32-byte digest
byte[] data = System.Text.Encoding.UTF8.GetBytes("hello world");
byte[] hash = Blake3Hasher.Hash(data);

// Hash string (UTF-8) → hex
string hex = Blake3Hasher.HashToString("hello world");
// e.g. "d74981efa70a0c0b14d123f472c6e570..."

// Verify digest (constant-time)
bool ok = Blake3Hasher.Verify(data, hash);
```

## Usage

### Hashing

| Method | Description |
|--------|-------------|
| `Hash(byte[] input)` | Hash and return a new 32-byte array. |
| `Hash(ReadOnlySpan<byte> input)` | Hash from span; returns 32-byte array. |
| `Hash(ReadOnlyMemory<byte> input)` | Hash from memory; returns 32-byte array. |
| `Hash(..., Span<byte> destination)` | Hash and write digest into `destination` (must be ≥ 32 bytes). |
| `Hash(string s)` | Hash UTF-8 bytes of the string; returns 32-byte array. |
| `Hash(ReadOnlySpan<char> chars)` | Hash UTF-8 encoding of the character span. |
| `HashToString(string input)` | Hash string (UTF-8) and return 64-character lowercase hex. |

Single-chunk and multi-chunk inputs use a scalar path; no extra allocations for the common case.

### Parallel hashing (large inputs)

For large buffers, use the parallel API to spread work across cores and use SIMD batches where supported:

| Method | Description |
|--------|-------------|
| `HashParallel(byte[] input)` | Hash with parallel chunk processing; returns 32-byte array. |
| `HashParallel(ReadOnlyMemory<byte> input)` | Same, from `ReadOnlyMemory<byte>`. |
| `HashParallel(..., Span<byte> destination)` | Parallel hash into `destination` (≥ 32 bytes). |
| `HashParallelCopy(ReadOnlySpan<byte> input)` | Copies input then runs parallel hash (use when you only have a span of a large buffer). |

Parallelism kicks in when the input is large enough to form multiple chunks (e.g. &gt; 4 KiB). For smaller inputs, the regular `Hash` methods are usually faster.

### Blake3Util (file and directory hashing)

For file-based workflows, register `Blake3Util` (e.g. via `Blake3UtilRegistrar`) and inject `IBlake3Util`. It can hash individual files, entire directories, or produce a single aggregate hash for a directory (useful for integrity checks of a folder’s contents).

| Method | Description |
|--------|-------------|
| `HashFile(path)` | Hash a file and return its BLAKE3 digest as a 64-character hex string. |
| `HashFileToByteArray(path)` | Hash a file and return the 32-byte digest. |
| `HashDirectory(path)` | Hash every file in a directory (recursive); returns a dictionary of file path → 32-byte digest. |
| `HashDirectoryToAggregateString(path)` | Hash all files in a directory (sorted by path), combine path + hash per file, then BLAKE3 the aggregate; returns a single 64-character hex string for the whole directory. |

All methods are async and accept an optional `CancellationToken`. Files that cannot be read (e.g. access denied) are skipped when hashing directories.

### Verification

All `Verify` overloads compare the computed BLAKE3 digest to an expected value in **constant time** (via `CryptographicOperations.FixedTimeEquals`). The expected digest must be 32 bytes (or 64 hex characters where applicable).

| Method | Description |
|--------|-------------|
| `Verify(ReadOnlySpan<byte> input, ReadOnlySpan<byte> expectedHash)` | Returns `true` if BLAKE3(input) equals `expectedHash` (32 bytes). |
| `Verify(byte[] input, byte[] expectedHash)` | Same for byte arrays. |
| `Verify(string input, ReadOnlySpan<byte> expectedHash)` | Hash UTF-8 string and compare to 32-byte digest. |
| `Verify(string input, string expectedHashHex)` | Hash UTF-8 string and compare to 64-char hex string (case-insensitive). |

## Output

- Digest length is **32 bytes** (256 bits), as per BLAKE3.
- Hex strings from `HashToString` are **64 lowercase** hex characters.