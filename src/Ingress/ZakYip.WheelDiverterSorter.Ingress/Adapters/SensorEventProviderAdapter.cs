using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Ingress.Adapters;

/// <summary>
/// 传感器事件提供者适配器
/// </summary>
/// <remarks>
/// 适配 IParcelDetectionService 到 ISensorEventProvider 接口。
/// 将 Ingress 层的具体实现适配为 Execution 层期望的抽象接口。
/// 
/// <para><b>架构角色</b>：</para>
/// 作为桥梁连接 Execution 层和 Ingress 层，
/// 使得 SortingOrchestrator 不需要直接依赖 Ingress 项目。
/// </remarks>
public sealed class SensorEventProviderAdapter : ISensorEventProvider
{
    private readonly IParcelDetectionService _parcelDetectionService;
    private readonly ILogger<SensorEventProviderAdapter> _logger;

    /// <summary>
    /// 初始化传感器事件提供者适配器
    /// </summary>
    /// <param name="parcelDetectionService">底层的包裹检测服务</param>
    /// <param name="logger">日志记录器</param>
    public SensorEventProviderAdapter(
        IParcelDetectionService parcelDetectionService,
        ILogger<SensorEventProviderAdapter> logger)
    {
        _parcelDetectionService = parcelDetectionService ?? throw new ArgumentNullException(nameof(parcelDetectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 订阅底层服务的事件并转发
        _parcelDetectionService.ParcelDetected += OnUnderlyingParcelDetected;
        _parcelDetectionService.DuplicateTriggerDetected += OnUnderlyingDuplicateTriggerDetected;
        
        _logger.LogDebug("SensorEventProviderAdapter 已创建并订阅底层服务事件");
    }

    /// <inheritdoc />
    public event EventHandler<ParcelDetectedEventArgs>? ParcelDetected;

    /// <inheritdoc />
    public event EventHandler<DuplicateTriggerEventArgs>? DuplicateTriggerDetected;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("通过适配器启动传感器事件监听...");
        await _parcelDetectionService.StartAsync(cancellationToken);
        _logger.LogInformation("适配器：传感器事件监听已启动");
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        _logger.LogDebug("通过适配器停止传感器事件监听...");
        await _parcelDetectionService.StopAsync();
        _logger.LogInformation("适配器：传感器事件监听已停止");
    }

    /// <summary>
    /// 处理底层服务的包裹检测事件并转发到 Execution 层
    /// </summary>
    /// <remarks>
    /// 由于现在使用统一的 ParcelDetectedEventArgs 类型，无需类型转换，直接转发事件。
    /// </remarks>
    private void OnUnderlyingParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        _logger.LogDebug("适配器收到底层包裹检测事件: ParcelId={ParcelId}, SensorId={SensorId}", 
            e.ParcelId, e.SensorId);

        // 直接触发 Execution 层的事件（无需类型转换，统一使用 ParcelDetectedEventArgs）
        ParcelDetected.SafeInvoke(this, e, _logger, nameof(ParcelDetected));
    }

    /// <summary>
    /// 处理底层服务的重复触发事件并转发到 Execution 层
    /// </summary>
    private void OnUnderlyingDuplicateTriggerDetected(object? sender, DuplicateTriggerEventArgs e)
    {
        _logger.LogDebug("适配器收到底层重复触发事件: ParcelId={ParcelId}, SensorId={SensorId}", 
            e.ParcelId, e.SensorId);

        // 直接触发 Execution 层的事件（无需类型转换，统一使用 DuplicateTriggerEventArgs）
        DuplicateTriggerDetected.SafeInvoke(this, e, _logger, nameof(DuplicateTriggerDetected));
    }
}
