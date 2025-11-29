using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 启动时执行路由-拓扑一致性检查的后台服务
/// </summary>
/// <remarks>
/// 在系统启动时执行一次性检查，验证所有路由配置中的 ChuteId 是否都能在拓扑中生成有效路径。
/// 检查结果记录到日志，但不阻止系统启动（除非配置为强制模式）。
/// </remarks>
public class RouteTopologyConsistencyCheckWorker : IHostedService
{
    private readonly IRouteTopologyConsistencyChecker _consistencyChecker;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ILogger<RouteTopologyConsistencyCheckWorker> _logger;
    private readonly IConfiguration _configuration;

    public RouteTopologyConsistencyCheckWorker(
        IRouteTopologyConsistencyChecker consistencyChecker,
        ISystemConfigurationRepository systemConfigRepository,
        ILogger<RouteTopologyConsistencyCheckWorker> logger,
        IConfiguration configuration)
    {
        _consistencyChecker = consistencyChecker ?? throw new ArgumentNullException(nameof(consistencyChecker));
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("========================================");
        _logger.LogInformation("开始执行路由-拓扑一致性检查");
        _logger.LogInformation("========================================");

        try
        {
            // 执行一致性检查
            var result = _consistencyChecker.CheckConsistency();

            // 记录检查结果详情
            _logger.LogInformation(
                "路由-拓扑一致性检查完成：" +
                "总路由配置数={TotalRoutes}, 有效配置数={ValidRoutes}, 无效配置数={InvalidRoutes}",
                result.TotalRouteChuteIds,
                result.ValidRouteChuteIds,
                result.InvalidRouteChuteIds.Count);

            // 如果存在不一致的配置，记录详细信息
            if (!result.IsConsistent)
            {
                _logger.LogWarning(
                    "⚠️  发现 {Count} 个路由配置无法在拓扑中生成有效路径（ChuteId 不存在于拓扑配置）：",
                    result.InvalidRouteChuteIds.Count);

                foreach (var invalidChuteId in result.InvalidRouteChuteIds)
                {
                    _logger.LogWarning("   - ChuteId: {ChuteId}", invalidChuteId);
                }

                // 获取并验证异常格口配置
                var systemConfig = _systemConfigRepository.Get();
                _logger.LogInformation(
                    "已配置的异常格口 ExceptionChuteId={ExceptionChuteId}，" +
                    "无法生成路径的包裹将自动路由到此格口",
                    systemConfig.ExceptionChuteId);

                // 检查是否配置为严格模式（阻止启动）
                var strictMode = _configuration.GetValue<bool>("RouteTopology:ConsistencyCheck:StrictMode", false);
                if (strictMode)
                {
                    var errorMessage = 
                        "路由-拓扑一致性检查失败，且配置为严格模式（StrictMode=true），系统启动已阻止。" +
                        $"请修复以下无效的路由配置后重新启动：[{string.Join(", ", result.InvalidRouteChuteIds)}]";
                    
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
                else
                {
                    _logger.LogWarning(
                        "系统配置为非严格模式（StrictMode=false），允许启动。" +
                        "运行时，无法生成路径的包裹将自动路由到异常格口 {ExceptionChuteId}。",
                        systemConfig.ExceptionChuteId);
                }
            }
            else
            {
                _logger.LogInformation("✅ 路由-拓扑一致性检查通过，所有路由配置都能生成有效路径");
            }
        }
        catch (InvalidOperationException)
        {
            // 严格模式下的一致性检查失败，向上抛出以阻止启动
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "路由-拓扑一致性检查时发生异常");
            
            // 非严格模式下，异常不阻止启动
            var strictMode = _configuration.GetValue<bool>("RouteTopology:ConsistencyCheck:StrictMode", false);
            if (strictMode)
            {
                throw;
            }
        }
        finally
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("路由-拓扑一致性检查流程结束");
            _logger.LogInformation("========================================");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // 无需执行任何清理操作
        return Task.CompletedTask;
    }
}
