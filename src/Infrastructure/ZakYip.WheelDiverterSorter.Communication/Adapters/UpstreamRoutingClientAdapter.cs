using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Communication.Adapters;

/// <summary>
/// 上游路由客户端适配器
/// </summary>
/// <remarks>
/// 适配 IRuleEngineClient 到 IUpstreamRoutingClient 接口。
/// 将 Communication 层的具体实现适配为 Execution 层期望的抽象接口。
/// 
/// <para><b>架构角色</b>：</para>
/// 作为桥梁连接 Execution 层和 Communication 层，
/// 使得 SortingOrchestrator 不需要直接依赖 Communication 项目。
/// </remarks>
public sealed class UpstreamRoutingClientAdapter : IUpstreamRoutingClient
{
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ILogger<UpstreamRoutingClientAdapter> _logger;

    /// <summary>
    /// 初始化上游路由客户端适配器
    /// </summary>
    /// <param name="ruleEngineClient">底层的 RuleEngine 通信客户端</param>
    /// <param name="logger">日志记录器</param>
    public UpstreamRoutingClientAdapter(
        IRuleEngineClient ruleEngineClient,
        ILogger<UpstreamRoutingClientAdapter> logger)
    {
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 订阅底层客户端的事件并转发
        _ruleEngineClient.ChuteAssignmentReceived += OnUnderlyingChuteAssignmentReceived;
        
        _logger.LogDebug("UpstreamRoutingClientAdapter 已创建并订阅底层客户端事件");
    }

    /// <inheritdoc />
    public bool IsConnected => _ruleEngineClient.IsConnected;

    /// <inheritdoc />
    public event EventHandler<ChuteAssignmentEventArgs>? ChuteAssignmentReceived;

    /// <inheritdoc />
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("通过适配器连接上游系统...");
        var result = await _ruleEngineClient.ConnectAsync(cancellationToken);
        
        if (result)
        {
            _logger.LogInformation("适配器：上游系统连接成功");
        }
        else
        {
            _logger.LogWarning("适配器：上游系统连接失败");
        }
        
        return result;
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        _logger.LogDebug("通过适配器断开上游系统连接...");
        await _ruleEngineClient.DisconnectAsync();
        _logger.LogInformation("适配器：上游系统已断开连接");
    }

    /// <inheritdoc />
    public async Task<bool> NotifyParcelDetectedAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("通过适配器通知上游包裹检测: ParcelId={ParcelId}", parcelId);
        var result = await _ruleEngineClient.NotifyParcelDetectedAsync(parcelId, cancellationToken);
        
        if (!result)
        {
            _logger.LogWarning("适配器：上游通知发送失败: ParcelId={ParcelId}", parcelId);
        }
        
        return result;
    }

    /// <summary>
    /// 处理底层客户端的格口分配事件并转发到 Execution 层
    /// </summary>
    private void OnUnderlyingChuteAssignmentReceived(object? sender, ChuteAssignmentNotificationEventArgs e)
    {
        _logger.LogDebug("适配器收到底层格口分配通知: ParcelId={ParcelId}, ChuteId={ChuteId}", 
            e.ParcelId, e.ChuteId);

        // 转换为 Execution 层的事件参数类型
        var executionArgs = new ChuteAssignmentEventArgs
        {
            ParcelId = e.ParcelId,
            ChuteId = e.ChuteId,
            NotificationTime = e.NotificationTime,
            Metadata = e.Metadata
        };

        // 触发 Execution 层的事件
        ChuteAssignmentReceived?.Invoke(this, executionArgs);
    }
}
