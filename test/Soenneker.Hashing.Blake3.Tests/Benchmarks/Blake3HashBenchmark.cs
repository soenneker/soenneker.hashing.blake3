using System;
using BenchmarkDotNet.Attributes;
using Blake3;
using SharpHash.Base;
using SharpHash.Interfaces;

namespace Soenneker.Hashing.Blake3.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class Blake3HashBenchmark
{
    private byte[] _data = null!;
    private byte[] _digest = null!;
    private IHash _sharp = null!;

    [Params(4)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[DataSize];
        new Random(42).NextBytes(_data);

        _digest = new byte[32];

        _sharp = HashFactory.CreateHash("Blake3");
    }

    // -------------------
    // No-alloc / write to buffer
    // -------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NoAlloc")]
    public void Soenneker_NoAlloc()
    {
        Blake3Hasher.Hash(_data, _digest); // (you may need to add this overload)
    }

    [Benchmark]
    [BenchmarkCategory("NoAlloc")]
    public void Blake3Net_NoAlloc()
    {
        Hash hash = Hasher.Hash(_data);
        hash.AsSpan().CopyTo(_digest);
    }

    [Benchmark]
    [BenchmarkCategory("NoAlloc")]
    public void Blake3Managed_NoAlloc()
    {
        global::Blake3.Managed.Hash hash = global::Blake3.Managed.Hasher.Hash(_data);
        hash.AsSpan().CopyTo(_digest);
    }

    [Benchmark]
    [BenchmarkCategory("NoAlloc")]
    public void SharpHash_NoAlloc()
    {
        _sharp.Initialize();
        _sharp.TransformBytes(_data);
        IHashResult? result = _sharp.TransformFinal();
        result.GetBytes().AsSpan().CopyTo(_digest); // still allocs unless SharpHash exposes span
    }

    // -------------------
    // Allocating convenience APIs
    // -------------------

    // [Benchmark]
    [BenchmarkCategory("Alloc")]
    public byte[] Soenneker_Alloc()
        => Blake3Hasher.Hash(_data);

    //[Benchmark]
    [BenchmarkCategory("Alloc")]
    public byte[] Blake3Net_Alloc()
        => Hasher.Hash(_data).AsSpan().ToArray();

    // [Benchmark]
    [BenchmarkCategory("Alloc")]
    public byte[] Blake3Managed_Alloc()
        => global::Blake3.Managed.Hasher.Hash(_data).AsSpan().ToArray();

    //   [Benchmark]
    [BenchmarkCategory("Alloc")]
    public byte[] SharpHash_Alloc()
    {
        _sharp.Initialize();
        _sharp.TransformBytes(_data);
        return _sharp.TransformFinal().GetBytes();
    }
}