using System;
using AwesomeAssertions;
using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.Hashing.Blake3.Tests;

[Collection("Collection")]
public sealed class Blake3UtilTests : FixturedUnitTest
{
    public Blake3UtilTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Hash_empty_bytes_returns_32_bytes()
    {
        byte[] result = Blake3Util.Hash(ReadOnlySpan<byte>.Empty);

        result.Should().NotBeNull();
        result.Should().HaveCount(32);
    }

    [Fact]
    public void Hash_empty_bytes_produces_non_zero_digest()
    {
        byte[] result = Blake3Util.Hash(ReadOnlySpan<byte>.Empty);

        result.Should().NotBeNull();
        result.Should().HaveCount(32);
        result.Should().NotBeEquivalentTo(new byte[32]);
    }

    [Fact]
    public void Hash_empty_string_equals_hash_empty_bytes()
    {
        byte[] fromString = Blake3Util.Hash("");
        byte[] fromBytes = Blake3Util.Hash(ReadOnlySpan<byte>.Empty);

        fromString.Should().BeEquivalentTo(fromBytes);
    }

    [Fact]
    public void Hash_string_abc_equals_hash_utf8_bytes_abc()
    {
        byte[] fromString = Blake3Util.Hash("abc");
        byte[] abcBytes = "abc"u8.ToArray();
        byte[] fromBytes = Blake3Util.Hash(abcBytes);

        fromString.Should().HaveCount(32);
        fromBytes.Should().HaveCount(32);
        fromBytes.Should().BeEquivalentTo(fromString);
    }

    [Fact]
    public void Hash_is_deterministic()
    {
        byte[] input = [1, 2, 3, 4, 5];
        byte[] first = Blake3Util.Hash(input);
        byte[] second = Blake3Util.Hash(input);

        first.Should().BeEquivalentTo(second);
    }

    [Fact]
    public void Hash_different_inputs_produce_different_hashes()
    {
        byte[] a = Blake3Util.Hash([1, 2, 3]);
        byte[] b = Blake3Util.Hash([1, 2, 4]);

        a.Should().NotBeEquivalentTo(b);
    }

    [Fact]
    public void HashParallel_small_input_matches_Hash()
    {
        byte[] input = [10, 20, 30, 40, 50];
        byte[] fromHash = Blake3Util.Hash(input);
        byte[] fromParallel = Blake3Util.HashParallel(input);

        fromHash.Should().BeEquivalentTo(fromParallel);
    }

    [Fact]
    public void HashParallel_empty_matches_Hash_empty()
    {
        byte[] fromHash = Blake3Util.Hash(ReadOnlySpan<byte>.Empty);
        byte[] fromParallel = Blake3Util.HashParallel(Array.Empty<byte>());

        fromHash.Should().BeEquivalentTo(fromParallel);
    }

    [Fact]
    public void HashParallelCopy_matches_Hash_for_same_input()
    {
        byte[] input = [7, 8, 9, 10];
        byte[] fromHash = Blake3Util.Hash(input);
        byte[] fromParallelCopy = Blake3Util.HashParallelCopy(input);

        fromHash.Should().BeEquivalentTo(fromParallelCopy);
    }

    [Fact]
    public void Hash_utf8_string_produces_32_byte_hash()
    {
        byte[] result = Blake3Util.Hash("Hello, 世界");

        result.Should().NotBeNull();
        result.Should().HaveCount(32);
    }

    [Fact]
    public void Hash_span_char_matches_string_overload()
    {
        const string s = "test";
        byte[] fromString = Blake3Util.Hash(s);
        byte[] fromSpan = Blake3Util.Hash(s.AsSpan());

        fromString.Should().BeEquivalentTo(fromSpan);
    }
}
