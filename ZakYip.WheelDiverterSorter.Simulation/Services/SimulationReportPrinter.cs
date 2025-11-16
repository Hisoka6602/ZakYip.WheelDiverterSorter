using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 仿真报告打印服务
/// </summary>
/// <remarks>
/// 负责输出仿真配置摘要和统计报告
/// </remarks>
public class SimulationReportPrinter
{
    private readonly ILogger<SimulationReportPrinter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SimulationReportPrinter(ILogger<SimulationReportPrinter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 打印仿真配置摘要
    /// </summary>
    public void PrintConfigurationSummary(SimulationOptions options)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("                   仿真配置摘要");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine($"包裹数量：          {options.ParcelCount}");
        Console.WriteLine($"线速：              {options.LineSpeedMmps} mm/s");
        Console.WriteLine($"包裹间隔：          {options.ParcelInterval.TotalSeconds:F2} 秒");
        Console.WriteLine($"分拣模式：          {options.SortingMode}");
        
        if (options.SortingMode == "FixedChute" && options.FixedChuteIds != null)
        {
            Console.WriteLine($"固定格口：          {string.Join(", ", options.FixedChuteIds)}");
        }
        
        Console.WriteLine($"异常格口：          {options.ExceptionChuteId}");
        Console.WriteLine($"随机故障注入：      {(options.IsEnableRandomFaultInjection ? "启用" : "禁用")}");
        
        if (options.IsEnableRandomFaultInjection)
        {
            Console.WriteLine($"故障注入概率：      {options.FaultInjectionProbability:P2}");
        }
        
        Console.WriteLine($"详细日志：          {(options.IsEnableVerboseLogging ? "启用" : "禁用")}");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    /// <summary>
    /// 打印仿真统计报告
    /// </summary>
    public void PrintStatisticsReport(SimulationStatistics statistics)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("                   仿真统计报告");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine($"总包裹数：          {statistics.TotalParcels}");
        Console.WriteLine($"成功分拣数：        {statistics.SuccessfulSorts}");
        Console.WriteLine($"失败分拣数：        {statistics.FailedSorts}");
        Console.WriteLine($"成功率：            {statistics.SuccessRate:P2}");
        Console.WriteLine($"总耗时：            {statistics.TotalDuration.TotalSeconds:F2} 秒");
        Console.WriteLine($"平均每包耗时：      {statistics.AverageTimePerParcel.TotalMilliseconds:F2} 毫秒");
        Console.WriteLine();
        Console.WriteLine("格口分拣统计：");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.WriteLine($"{"格口ID",-15} {"分拣数量",-15} {"百分比",-15}");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        
        foreach (var (chuteId, count) in statistics.ChuteStatistics.OrderByDescending(x => x.Value))
        {
            var percentage = (double)count / statistics.TotalParcels * 100.0;
            Console.WriteLine($"{chuteId,-15} {count,-15} {percentage:F2}%");
        }
        
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        _logger.LogInformation(
            "仿真完成：总包裹数={TotalParcels}, 成功={Success}, 失败={Failed}, 成功率={SuccessRate:P2}",
            statistics.TotalParcels,
            statistics.SuccessfulSorts,
            statistics.FailedSorts,
            statistics.SuccessRate);
    }
}

/// <summary>
/// 仿真统计数据
/// </summary>
public class SimulationStatistics
{
    /// <summary>
    /// 总包裹数
    /// </summary>
    public int TotalParcels { get; set; }

    /// <summary>
    /// 成功分拣数
    /// </summary>
    public int SuccessfulSorts { get; set; }

    /// <summary>
    /// 失败分拣数
    /// </summary>
    public int FailedSorts { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalParcels > 0 ? (double)SuccessfulSorts / TotalParcels : 0.0;

    /// <summary>
    /// 总耗时
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// 平均每包耗时
    /// </summary>
    public TimeSpan AverageTimePerParcel => TotalParcels > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalParcels) : TimeSpan.Zero;

    /// <summary>
    /// 格口统计（格口ID -> 分拣数量）
    /// </summary>
    public Dictionary<int, int> ChuteStatistics { get; set; } = new Dictionary<int, int>();
}
