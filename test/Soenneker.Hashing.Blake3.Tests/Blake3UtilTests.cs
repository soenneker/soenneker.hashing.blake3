using System;
using AwesomeAssertions;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Hashing.Blake3.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class Blake3UtilTests : HostedUnitTest
{
    public Blake3UtilTests(Host host) : base(host)
    {
    }

    [Test]
    public void Hash_empty_bytes_returns_32_bytes()
    {
        byte[] result = Blake3Hasher.Hash(ReadOnlySpan<byte>.Empty);

        result.Should()
              .NotBeNull();
        result.Should()
              .HaveCount(32);
    }

    [Test]
    public void Hash_empty_bytes_produces_non_zero_digest()
    {
        byte[] result = Blake3Hasher.Hash(ReadOnlySpan<byte>.Empty);

        result.Should()
              .NotBeNull();
        result.Should()
              .HaveCount(32);
        result.Should()
              .NotBeEquivalentTo(new byte[32]);
    }

    [Test]
    public void Hash_empty_string_equals_hash_empty_bytes()
    {
        byte[] fromString = Blake3Hasher.Hash("");
        byte[] fromBytes = Blake3Hasher.Hash(ReadOnlySpan<byte>.Empty);

        fromString.Should()
                  .BeEquivalentTo(fromBytes);
    }

    [Test]
    public void Hash_string_abc_equals_hash_utf8_bytes_abc()
    {
        byte[] fromString = Blake3Hasher.Hash("abc");
        byte[] abcBytes = "abc"u8.ToArray();
        byte[] fromBytes = Blake3Hasher.Hash(abcBytes);

        fromString.Should()
                  .HaveCount(32);
        fromBytes.Should()
                 .HaveCount(32);
        fromBytes.Should()
                 .BeEquivalentTo(fromString);
    }

    [Test]
    public void Hash_is_deterministic()
    {
        byte[] input = [1, 2, 3, 4, 5];
        byte[] first = Blake3Hasher.Hash(input);
        byte[] second = Blake3Hasher.Hash(input);

        first.Should()
             .BeEquivalentTo(second);
    }

    [Test]
    public void Hash_different_inputs_produce_different_hashes()
    {
        byte[] a = Blake3Hasher.Hash([1, 2, 3]);
        byte[] b = Blake3Hasher.Hash([1, 2, 4]);

        a.Should()
         .NotBeEquivalentTo(b);
    }

    [Test]
    public void HashParallel_small_input_matches_Hash()
    {
        byte[] input = [10, 20, 30, 40, 50];
        byte[] fromHash = Blake3Hasher.Hash(input);
        byte[] fromParallel = Blake3Hasher.HashParallel(input);

        fromHash.Should()
                .BeEquivalentTo(fromParallel);
    }

    [Test]
    public void HashParallel_empty_matches_Hash_empty()
    {
        byte[] fromHash = Blake3Hasher.Hash(ReadOnlySpan<byte>.Empty);
        byte[] fromParallel = Blake3Hasher.HashParallel(Array.Empty<byte>());

        fromHash.Should()
                .BeEquivalentTo(fromParallel);
    }

    [Test]
    public void HashParallelCopy_matches_Hash_for_same_input()
    {
        byte[] input = [7, 8, 9, 10];
        byte[] fromHash = Blake3Hasher.Hash(input);
        byte[] fromParallelCopy = Blake3Hasher.HashParallelCopy(input);

        fromHash.Should()
                .BeEquivalentTo(fromParallelCopy);
    }

    [Test]
    public void Hash_utf8_string_produces_32_byte_hash()
    {
        byte[] result = Blake3Hasher.Hash("Hello, 世界");

        result.Should()
              .NotBeNull();
        result.Should()
              .HaveCount(32);
    }

    [Test]
    public void Hash_span_char_matches_string_overload()
    {
        const string s = "test";
        byte[] fromString = Blake3Hasher.Hash(s);
        byte[] fromSpan = Blake3Hasher.Hash(s.AsSpan());

        fromString.Should()
                  .BeEquivalentTo(fromSpan);
    }

    [Test]
    public void HashToString_does_not_throw()
    {
        const string s = "test";
        string toString = Blake3Hasher.HashToString(s);
        toString.Should()
                .NotBeNull();
    }
}