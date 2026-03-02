using BenchmarkDotNet.Reports;
using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;
using System.Threading.Tasks;
using Xunit;

namespace Soenneker.Hashing.Blake3.Tests.Benchmarks;

public class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

  //  [LocalFact]
    public async ValueTask Blake3HashBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<Blake3HashBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }
}