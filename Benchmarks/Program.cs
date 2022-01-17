using Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Exporters;
using Perfolizer.Horology;

BenchmarkSwitcher.FromTypes(new[]
{
    typeof(Encode),
    typeof(Decode)
}).RunAllJoined(new Config());

class Config : ManualConfig
{
    public Config()
    {
        AddJob(Job.ShortRun
            .WithLaunchCount(1)
            .WithWarmupCount(6)
            .WithIterationTime(TimeInterval.FromSeconds(1))
            .WithIterationCount(6)
            .WithToolchain(InProcessEmitToolchain.Instance));

        AddLogger(new ConsoleLogger());

        AddExporter(HtmlExporter.Default);

        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}
