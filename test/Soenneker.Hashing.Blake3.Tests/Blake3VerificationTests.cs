using System;
using AwesomeAssertions;
using SharpHash.Base;
using SharpHash.Interfaces;

namespace Soenneker.Hashing.Blake3.Tests;

public sealed class Blake3VerificationTests
{
    private static byte[] GetInput()
    {
        var bytes = new byte[256];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    [Test]
    public void Soenneker_matches_Blake3Net()
    {
        byte[] input = GetInput();
        byte[] a = Blake3Hasher.Hash(input);
        byte[] b = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();
        a.Should().BeEquivalentTo(b);
    }

    [Test]
    public void Soenneker_matches_Blake3Managed()
    {
        byte[] input = GetInput();
        byte[] a = Blake3Hasher.Hash(input);
        byte[] b = global::Blake3.Managed.Hasher.Hash(input).AsSpan().ToArray();
        a.Should().BeEquivalentTo(b);
    }

    [Test]
    public void Soenneker_matches_SharpHash()
    {
        byte[] input = GetInput();
        byte[] a = Blake3Hasher.Hash(input);
        byte[] b = HashWithSharpHash(input);
        a.Should().BeEquivalentTo(b);
    }

    [Test]
    public void Blake3Net_matches_Blake3Managed()
    {
        byte[] input = GetInput();
        byte[] a = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();
        byte[] b = global::Blake3.Managed.Hasher.Hash(input).AsSpan().ToArray();
        a.Should().BeEquivalentTo(b);
    }

    [Test]
    public void Blake3Net_matches_SharpHash()
    {
        byte[] input = GetInput();
        byte[] a = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();
        byte[] b = HashWithSharpHash(input);
        a.Should().BeEquivalentTo(b);
    }

    [Test]
    public void Blake3Managed_matches_SharpHash()
    {
        byte[] input = GetInput();
        byte[] a = global::Blake3.Managed.Hasher.Hash(input).AsSpan().ToArray();
        byte[] b = HashWithSharpHash(input);
        a.Should().BeEquivalentTo(b);
    }

    [Test]
    public void Hash_empty_matches_Blake3_NuGet()
    {
        byte[] input = Array.Empty<byte>();
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().HaveCount(32);
        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void Hash_empty_matches_Blake3_Managed()
    {
        byte[] input = Array.Empty<byte>();
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Managed.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().HaveCount(32);
        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void Hash_abc_bytes_matches_Blake3_NuGet()
    {
        byte[] input = "abc"u8.ToArray();
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void Hash_abc_bytes_matches_Blake3_Managed()
    {
        byte[] input = "abc"u8.ToArray();
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Managed.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void Hash_arbitrary_bytes_matches_Blake3_NuGet()
    {
        byte[] input = [1, 2, 3, 4, 5, 100, 200, 250];
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void Hash_arbitrary_bytes_matches_Blake3_Managed()
    {
        byte[] input = [1, 2, 3, 4, 5, 100, 200, 250];
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Managed.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void Hash_1024_bytes_matches_Blake3_NuGet()
    {
        byte[] input = Blake3TestVectors.GetTestInput(1024);
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void Hash_1024_bytes_matches_Blake3_Managed()
    {
        byte[] input = Blake3TestVectors.GetTestInput(1024);
        byte[] ours = Blake3Hasher.Hash(input);
        byte[] other = global::Blake3.Managed.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().BeEquivalentTo(other);
    }

    [Test]
    public void HashParallel_same_input_matches_Blake3_NuGet()
    {
        byte[] input = Blake3TestVectors.GetTestInput(2048);
        byte[] ours = Blake3Hasher.HashParallel(input);
        byte[] other = global::Blake3.Hasher.Hash(input).AsSpan().ToArray();

        ours.Should().BeEquivalentTo(other);
    }

    // --- Official BLAKE3 test vectors (BLAKE3-team/BLAKE3 test_vectors.json) ---

    [Test]
    public void Hash_empty_matches_official_test_vector()
    {
        byte[] expected = Convert.FromHexString("af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262");
        byte[] input = Array.Empty<byte>();
        byte[] actual = Blake3Hasher.Hash(input);

        actual.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Hash_test_vector_len_3_matches_official()
    {
        byte[] expected = Convert.FromHexString("e1be4d7a8ab5560aa4199eea339849ba8e293d55ca0a81006726d184519e647f");
        byte[] input = Blake3TestVectors.GetTestInput(3);
        byte[] actual = Blake3Hasher.Hash(input);

        actual.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Hash_test_vector_len_64_matches_official()
    {
        byte[] expected = Convert.FromHexString("4eed7141ea4a5cd4b788606bd23f46e212af9cacebacdc7d1f4c6dc7f2511b98");
        byte[] input = Blake3TestVectors.GetTestInput(64);
        byte[] actual = Blake3Hasher.Hash(input);

        actual.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Hash_test_vector_len_1024_matches_official()
    {
        byte[] expected = Convert.FromHexString("42214739f095a406f3fc83deb889744ac00df831c10daa55189b5d121c855af7");
        byte[] input = Blake3TestVectors.GetTestInput(1024);
        byte[] actual = Blake3Hasher.Hash(input);

        actual.Should().BeEquivalentTo(expected);
    }

    private static byte[] HashWithSharpHash(byte[] input)
    {
        IHash hash = HashFactory.CreateHash("Blake3");
        hash.Initialize();
        hash.TransformBytes(input);
        return hash.TransformFinal().GetBytes();
    }
}
