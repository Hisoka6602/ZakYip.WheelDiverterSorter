using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 传感器监听演示服务
/// </summary>
/// <remarks>
/// 用于演示传感器监听和包裹检测功能。
/// 在生产环境中，此服务会自动启动并持续监听传感器事件。
/// </remarks>
public class SensorMonitoringWorker : BackgroundService
{
    private readonly IParcelDetectionService _parcelDetectionService;
    private readonly ILogger<SensorMonitoringWorker> _logger;
    private readonly ISafeExecutionService _safeExecutor;

    public SensorMonitoringWorker(
        IParcelDetectionService parcelDetectionService,
        ILogger<SensorMonitoringWorker> logger,
        ISafeExecutionService safeExecutor)
    {
        _parcelDetectionService = parcelDetectionService;
        _logger = logger;
        _safeExecutor = safeExecutor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("传感器监听服务启动");

        // 订阅包裹检测事件
        _parcelDetectionService.ParcelDetected += OnParcelDetected;

        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                // 启动包裹检测服务
                await _parcelDetectionService.StartAsync(stoppingToken);

                // 保持运行直到取消
                await Task.Delay(Timeout.Infinite, stoppingToken);
            },
            "SensorMonitoring",
            stoppingToken);

        _parcelDetectionService.ParcelDetected -= OnParcelDetected;
        await _parcelDetectionService.StopAsync();
    }

    /// <summary>
    /// 处理包裹检测事件
    /// </summary>
    private void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        _logger.LogInformation(
            "【包裹检测】包裹ID: {ParcelId}, 检测时间: {DetectedAt}, 传感器: {SensorId} ({SensorType})",
            e.ParcelId,
            e.DetectedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            e.SensorId,
            e.SensorType);

        // 在生产环境中，这里应该：
        // 1. 将包裹记录保存到数据库
        // 2. 通过TCP/SignalR/MQTT向RuleEngine请求格口号
        // 3. 接收格口号后，调用路径生成和执行服务进行分拣
    }
}
