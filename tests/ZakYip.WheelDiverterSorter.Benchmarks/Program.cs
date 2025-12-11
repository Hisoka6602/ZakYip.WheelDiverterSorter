using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using BenchmarkDotNet.Running;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
