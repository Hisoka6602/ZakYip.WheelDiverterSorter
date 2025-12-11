using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Simulation.Cli.Clients;

/// <summary>
/// 仿真用的上游路由客户端
/// </summary>
/// <remarks>
/// 用于仿真环境，不进行真实的网络通信。
/// 支持三种模式：Formal（从配置获取格口）、FixedChute（固定格口）、RoundRobin（轮询）
/// 
/// NOTE: 此实现仅用于 Simulation.Cli 项目，不应在生产代码中使用。
/// </remarks>
public sealed class SimulatedUpstreamRoutingClient : IUpstreamRoutingClient, IDisposable
{
    private readonly ILogger<SimulatedUpstreamRoutingClient>? _logger;
    private readonly ISystemClock _systemClock;
    private readonly Func<long, int> _chuteAssignmentFunc;
    private bool _isConnected;
    private bool _isDisposed;

    /// <summary>
    /// 格口分配事件
    /// </summary>
    public event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chuteAssignmentFunc">格口分配函数，输入包裹ID，返回格口号</param>
    /// <param name="systemClock">系统时钟</param>
    /// <param name="logger">日志记录器</param>
    public SimulatedUpstreamRoutingClient(
        Func<long, int> chuteAssignmentFunc,
        ISystemClock systemClock,
        ILogger<SimulatedUpstreamRoutingClient>? logger = null)
    {
        _chuteAssignmentFunc = chuteAssignmentFunc ?? throw new ArgumentNullException(nameof(chuteAssignmentFunc));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _logger = logger;
    }

    /// <summary>
    /// 连接到上游系统（模拟实现）
    /// </summary>
    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SimulatedUpstreamRoutingClient));
        }

        _logger?.LogInformation("仿真客户端：已连接");
        _isConnected = true;
        return Task.FromResult(true);
    }

    /// <summary>
    /// 断开与上游系统的连接（模拟实现）
    /// </summary>
    public Task DisconnectAsync()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SimulatedUpstreamRoutingClient));
        }

        _logger?.LogInformation("仿真客户端：已断开连接");
        _isConnected = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 请求包裹路由（模拟实现）
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 重命名为 NotifyParcelDetectedAsync（原RequestRoutingAsync）
    /// </remarks>
    public Task<bool> NotifyParcelDetectedAsync(long parcelId, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SimulatedUpstreamRoutingClient));
        }

        if (!_isConnected)
        {
            _logger?.LogWarning("仿真客户端：未连接，无法发送包裹检测通知");
            return Task.FromResult(false);
        }

        try
        {
            // 使用格口分配函数计算目标格口
            var targetChuteId = _chuteAssignmentFunc(parcelId);
            
            _logger?.LogInformation(
                "仿真客户端：包裹 {ParcelId} 检测通知已发送，分配格口 {ChuteId}",
                parcelId,
                targetChuteId);

            // 触发格口分配事件（模拟上游系统响应）
            ChuteAssigned.SafeInvoke(this, new ChuteAssignmentEventArgs
            {
                ParcelId = parcelId,
                ChuteId = targetChuteId,
                AssignedAt = _systemClock.LocalNowOffset
            }, _logger, nameof(ChuteAssigned));

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "仿真客户端：包裹 {ParcelId} 检测通知发送失败", parcelId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 通知包裹分拣完成（模拟实现）
    /// </summary>
    public Task<bool> NotifySortingCompletedAsync(
        Core.Abstractions.Upstream.SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SimulatedUpstreamRoutingClient));
        }

        _logger?.LogDebug(
            "仿真客户端：包裹 {ParcelId} 分拣完成通知已发送（模拟）",
            notification.ParcelId);

        return Task.FromResult(true);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isConnected = false;
        _isDisposed = true;
    }
}
