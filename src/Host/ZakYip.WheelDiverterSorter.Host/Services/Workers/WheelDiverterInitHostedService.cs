using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// 摆轮初始化后台服务
/// </summary>
/// <remarks>
/// 在系统启动时连接所有配置的摆轮设备。
/// 该服务在BootHostedService之后执行，确保基础设施已就绪。
/// </remarks>
public sealed class WheelDiverterInitHostedService : IHostedService
{
    private readonly IWheelDiverterConnectionService _connectionService;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<WheelDiverterInitHostedService> _logger;

    public WheelDiverterInitHostedService(
        IWheelDiverterConnectionService connectionService,
        ISafeExecutionService safeExecutor,
        ILogger<WheelDiverterInitHostedService> logger)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 启动时连接摆轮设备
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("========== 摆轮设备初始化 ==========");

                try
                {
                    var result = await _connectionService.ConnectAllAsync(cancellationToken);

                    if (result.IsSuccess)
                    {
                        _logger.LogInformation(
                            "✅ 摆轮设备连接成功: {ConnectedCount}/{TotalCount} 台",
                            result.ConnectedCount, result.TotalCount);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "⚠️ 摆轮设备部分连接: 成功={ConnectedCount}/{TotalCount}, 失败={FailedCount}",
                            result.ConnectedCount, result.TotalCount, result.FailedDriverIds.Count);

                        if (result.FailedDriverIds.Any())
                        {
                            _logger.LogWarning(
                                "失败的摆轮ID: {FailedIds}",
                                string.Join(", ", result.FailedDriverIds));
                        }

                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            _logger.LogWarning("错误信息: {ErrorMessage}", result.ErrorMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ 摆轮设备初始化失败");
                }

                _logger.LogInformation("========================================");
            },
            operationName: "WheelDiverterInitialization",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 停止服务（无操作）
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WheelDiverterInit service停止");
        return Task.CompletedTask;
    }
}
