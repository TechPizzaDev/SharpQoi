using Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Exporters;

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
            .WithToolchain(InProcessEmitToolchain.Instance));

        AddLogger(new ConsoleLogger());

        AddExporter(HtmlExporter.Default);

        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}
