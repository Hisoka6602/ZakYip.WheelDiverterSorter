using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 启动时路由-拓扑一致性检查服务
/// </summary>
/// <remarks>
/// 在应用启动时执行一致性检查，确保路由配置与拓扑配置的一致性
/// </remarks>
public class StartupConsistencyCheckService : IHostedService
{
    private readonly IRouteTopologyConsistencyChecker _consistencyChecker;
    private readonly ILineTopologyRepository _topologyRepository;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<StartupConsistencyCheckService> _logger;

    public StartupConsistencyCheckService(
        IRouteTopologyConsistencyChecker consistencyChecker,
        ILineTopologyRepository topologyRepository,
        ISystemConfigurationRepository systemConfigRepository,
        ISystemClock clock,
        ILogger<StartupConsistencyCheckService> logger)
    {
        _consistencyChecker = consistencyChecker ?? throw new ArgumentNullException(nameof(consistencyChecker));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("==================== 启动时配置一致性检查 ====================");
        _logger.LogInformation("开始检查路由与拓扑配置的一致性...");

        try
        {
            // 1. 验证异常格口配置
            ValidateExceptionChuteConfiguration();

            // 2. 执行路由-拓扑一致性检查
            var result = _consistencyChecker.Check();

            // 3. 输出检查结果汇总
            LogCheckSummary(result);

            _logger.LogInformation("==================== 配置一致性检查完成 ====================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动时配置一致性检查失败");
            // 不抛出异常，允许应用继续启动（降级运行）
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // 无需清理资源
        return Task.CompletedTask;
    }

    /// <summary>
    /// 验证异常格口配置
    /// </summary>
    private void ValidateExceptionChuteConfiguration()
    {
        var systemConfig = _systemConfigRepository.Get();
        var exceptionChuteId = systemConfig.ExceptionChuteId;

        _logger.LogInformation("异常格口配置: ExceptionChuteId = {ExceptionChuteId}", exceptionChuteId);

        // 检查异常格口是否在拓扑中存在
        var topology = _topologyRepository.Get();
        var exceptionChuteIdStr = exceptionChuteId.ToString();
        var exceptionChute = topology.Chutes.FirstOrDefault(c => c.ChuteId == exceptionChuteIdStr);

        if (exceptionChute == null)
        {
            _logger.LogError(
                "严重配置错误：异常格口 {ExceptionChuteId} 在拓扑配置中不存在！" +
                "这可能导致系统无法正常处理异常包裹。" +
                "请检查系统配置和拓扑配置。",
                exceptionChuteId);
        }
        else
        {
            _logger.LogInformation(
                "异常格口验证通过: ChuteId={ChuteId}, ChuteName={ChuteName}, " +
                "BoundNode={BoundNode}, Direction={Direction}, Enabled={Enabled}",
                exceptionChute.ChuteId,
                exceptionChute.ChuteName,
                exceptionChute.BoundNodeId,
                exceptionChute.BoundDirection,
                exceptionChute.IsEnabled);

            if (!exceptionChute.IsEnabled)
            {
                _logger.LogWarning(
                    "警告：异常格口 {ExceptionChuteId} 在拓扑中被标记为禁用状态。" +
                    "这可能导致异常包裹无法正常分拣。",
                    exceptionChuteId);
            }
        }
    }

    /// <summary>
    /// 输出检查结果汇总
    /// </summary>
    private void LogCheckSummary(RouteTopologyConsistencyResult result)
    {
        _logger.LogInformation("===============================================");
        _logger.LogInformation("路由-拓扑一致性检查结果汇总");
        _logger.LogInformation("===============================================");
        _logger.LogInformation("检查时间: {CheckTime:yyyy-MM-dd HH:mm:ss}", result.CheckedAt);
        _logger.LogInformation("一致性状态: {Status}", result.IsConsistent ? "✓ 通过" : "✗ 不一致");
        _logger.LogInformation("路由配置格口总数: {Count}", result.RoutingChuteIds.Count);
        _logger.LogInformation("拓扑配置格口总数: {Count}", result.TopologyChuteIds.Count);
        _logger.LogInformation("非法引用格口数: {Count}", result.InvalidRoutingReferences.Count);
        _logger.LogInformation("未使用拓扑格口数: {Count}", result.UnusedTopologyChutes.Count);

        if (result.InvalidRoutingReferences.Count > 0)
        {
            _logger.LogWarning("-----------------------------------------------");
            _logger.LogWarning("⚠ 发现 {Count} 个非法引用的格口:", result.InvalidRoutingReferences.Count);
            foreach (var chuteId in result.InvalidRoutingReferences.OrderBy(id => id))
            {
                _logger.LogWarning("  - ChuteId: {ChuteId} (路由配置中存在，但拓扑中不存在)", chuteId);
            }
            _logger.LogWarning("运行时影响：这些格口的包裹将被强制路由到异常格口");
            _logger.LogWarning("建议：检查并修正路由配置或拓扑配置");
        }

        if (result.UnusedTopologyChutes.Count > 0)
        {
            _logger.LogInformation("-----------------------------------------------");
            _logger.LogInformation("ℹ 发现 {Count} 个未使用的拓扑格口:", result.UnusedTopologyChutes.Count);
            foreach (var chuteId in result.UnusedTopologyChutes.OrderBy(id => id))
            {
                _logger.LogInformation("  - ChuteId: {ChuteId} (拓扑中存在，但路由未配置)", chuteId);
            }
            _logger.LogInformation("说明：这些格口可能是预留的或暂未配置路由规则");
        }

        _logger.LogInformation("===============================================");
    }
}
