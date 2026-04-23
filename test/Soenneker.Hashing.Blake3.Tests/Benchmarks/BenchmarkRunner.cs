namespace Soenneker.Hashing.Blake3.Tests.Benchmarks;

public class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner() : base()
    {
    }

  //  [LocalOnly]
    public async ValueTask Blake3HashBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<Blake3HashBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog();
    }
}


