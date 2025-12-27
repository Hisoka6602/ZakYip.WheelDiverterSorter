using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using BenchmarkDotNet.Running;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // 检查是否运行快速测试
        if (args.Length > 0 && args[0] == "--quick-queue-test")
        {
            QuickQueuePerformanceTest.Run(args);
            return;
        }
        
        var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
