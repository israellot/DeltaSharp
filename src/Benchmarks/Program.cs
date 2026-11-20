using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System.Linq;

namespace SharpFossilBenchmarks;

class Program
{
    public static int Main(string[] args)
    {
#if DELTASHARP_HASHTABLE24
        const string enable24BitHashtable = "true";
#else
        const string enable24BitHashtable = "false";
#endif
        // BenchmarkDotNet rebuilds the project in a separate process. Carry the
        // compile flag into that build, including jobs selected on the CLI.
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithArguments(new Argument[]
            {
                new MsBuildArgument($"/p:Enable24BitHashtable={enable24BitHashtable}")
            })
                .AsMutator());

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
            .Run(args.Length == 0 ? new[] { "--filter", "*PatchOperations*" } : args, config);

        return summaries.Any(summary => summary.HasCriticalValidationErrors ||
            summary.Reports.Any(report => !report.Success)) ? 1 : 0;
    }
}
