using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;

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
        Console.WriteLine($"摩擦模拟：          {(options.IsEnableRandomFriction ? "启用" : "禁用")}");
        
        if (options.IsEnableRandomFriction)
        {
            Console.WriteLine($"  - 摩擦因子范围：  {options.FrictionModel.MinFactor:F2} - {options.FrictionModel.MaxFactor:F2}");
            Console.WriteLine($"  - 确定性模式：    {(options.FrictionModel.IsDeterministic ? "是" : "否")}");
        }
        
        Console.WriteLine($"掉包模拟：          {(options.IsEnableRandomDropout ? "启用" : "禁用")}");
        
        if (options.IsEnableRandomDropout)
        {
            Console.WriteLine($"  - 掉包概率：      {options.DropoutModel.DropoutProbabilityPerSegment:P2}");
            if (options.DropoutModel.AllowedSegments != null && options.DropoutModel.AllowedSegments.Count > 0)
            {
                Console.WriteLine($"  - 允许段：        {string.Join(", ", options.DropoutModel.AllowedSegments)}");
            }
        }
        
        Console.WriteLine($"详细日志：          {(options.IsEnableVerboseLogging ? "启用" : "禁用")}");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    /// <summary>
    /// 打印仿真统计报告
    /// </summary>
    public void PrintStatisticsReport(SimulationSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("                   仿真统计报告");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine($"总包裹数：                {summary.TotalParcels}");
        Console.WriteLine($"成功分拣到目标格口：      {summary.SortedToTargetChuteCount}");
        Console.WriteLine($"超时：                    {summary.TimeoutCount}");
        Console.WriteLine($"掉包：                    {summary.DroppedCount}");
        Console.WriteLine($"执行错误：                {summary.ExecutionErrorCount}");
        Console.WriteLine($"规则引擎超时：            {summary.RuleEngineTimeoutCount}");
        Console.WriteLine($"分拣到错误格口：          {summary.SortedToWrongChuteCount} {(summary.SortedToWrongChuteCount > 0 ? "⚠️ 警告！" : "✓")}");
        Console.WriteLine($"成功率：                  {summary.SuccessRate:P2}");
        Console.WriteLine($"总耗时：                  {summary.TotalDuration.TotalSeconds:F2} 秒");
        Console.WriteLine($"平均每包处理时间：        {summary.AverageTimePerParcel.TotalMilliseconds:F2} 毫秒");
        
        if (summary.AverageTravelTime.HasValue)
        {
            Console.WriteLine($"平均行程时间：            {summary.AverageTravelTime.Value.TotalMilliseconds:F2} 毫秒");
            Console.WriteLine($"最小行程时间：            {summary.MinTravelTime?.TotalMilliseconds:F2} 毫秒");
            Console.WriteLine($"最大行程时间：            {summary.MaxTravelTime?.TotalMilliseconds:F2} 毫秒");
        }
        
        Console.WriteLine();
        Console.WriteLine("状态分布统计：");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.WriteLine($"{"状态",-30} {"数量",-15} {"百分比",-15}");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        
        foreach (var (status, count) in summary.StatusStatistics.OrderByDescending(x => x.Value))
        {
            var percentage = (double)count / summary.TotalParcels * 100.0;
            var statusName = GetStatusDisplayName(status);
            Console.WriteLine($"{statusName,-30} {count,-15} {percentage:F2}%");
        }
        
        Console.WriteLine();
        Console.WriteLine("格口分拣统计：");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.WriteLine($"{"格口ID",-15} {"分拣数量",-15} {"百分比",-15}");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        
        foreach (var (chuteId, count) in summary.ChuteStatistics.OrderByDescending(x => x.Value))
        {
            var percentage = (double)count / summary.TotalParcels * 100.0;
            Console.WriteLine($"{chuteId,-15} {count,-15} {percentage:F2}%");
        }
        
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        // 验证"不允许错分"约束
        if (summary.SortedToWrongChuteCount > 0)
        {
            _logger.LogError(
                "⚠️ 仿真发现错分情况！SortedToWrongChuteCount = {Count}，这不应该发生！",
                summary.SortedToWrongChuteCount);
        }

        _logger.LogInformation(
            "仿真完成：总包裹数={TotalParcels}, 成功={Success}, 超时={Timeout}, 掉包={Dropped}, 成功率={SuccessRate:P2}",
            summary.TotalParcels,
            summary.SortedToTargetChuteCount,
            summary.TimeoutCount,
            summary.DroppedCount,
            summary.SuccessRate);
    }

    /// <summary>
    /// 获取状态的显示名称（中文）
    /// </summary>
    private string GetStatusDisplayName(ParcelSimulationStatus status)
    {
        return status switch
        {
            ParcelSimulationStatus.SortedToTargetChute => "成功分拣到目标格口",
            ParcelSimulationStatus.Timeout => "超时",
            ParcelSimulationStatus.Dropped => "掉包",
            ParcelSimulationStatus.ExecutionError => "执行错误",
            ParcelSimulationStatus.RuleEngineTimeout => "规则引擎超时",
            ParcelSimulationStatus.SortedToWrongChute => "错误分拣 ⚠️",
            _ => status.ToString()
        };
    }
}

/// <summary>
/// 仿真统计数据（已废弃，使用 SimulationSummary 代替）
/// </summary>
[Obsolete("使用 ZakYip.WheelDiverterSorter.Simulation.Results.SimulationSummary 代替")]
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
