using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 内存模拟的上游路由客户端
/// </summary>
/// <remarks>
/// 用于仿真和测试，不进行真实的网络通信。
/// 支持三种模式：Formal（从配置获取格口）、FixedChute（固定格口）、RoundRobin（轮询）
/// PR-U1: 直接实现 IUpstreamRoutingClient
/// PR-UPSTREAM02: 更新事件名和添加落格完成通知
/// </remarks>
public class InMemoryRuleEngineClient : IUpstreamRoutingClient, IDisposable
{
    private readonly ILogger<InMemoryRuleEngineClient>? _logger;
    private readonly ISystemClock _systemClock;
    private readonly Func<long, int> _chuteAssignmentFunc;
    private bool _isConnected;
    private bool _isDisposed;

    /// <summary>
    /// 格口分配事件
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 从 ChuteAssignmentReceived 重命名为 ChuteAssigned
    /// </remarks>
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
    public InMemoryRuleEngineClient(
        Func<long, int> chuteAssignmentFunc,
        ISystemClock systemClock,
        ILogger<InMemoryRuleEngineClient>? logger = null)
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
            throw new ObjectDisposedException(nameof(InMemoryRuleEngineClient));
        }

        _logger?.LogInformation("内存模拟客户端：已连接");
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
            throw new ObjectDisposedException(nameof(InMemoryRuleEngineClient));
        }

        _logger?.LogInformation("内存模拟客户端：已断开连接");
        _isConnected = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通知上游系统包裹已到达（模拟实现）
    /// </summary>
    public async Task<bool> NotifyParcelDetectedAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryRuleEngineClient));
        }

        if (!_isConnected)
        {
            _logger?.LogWarning("内存模拟客户端：未连接，无法通知包裹到达");
            return false;
        }

        _logger?.LogDebug("内存模拟客户端：收到包裹通知 {ParcelId}", parcelId);

        // 模拟一个短暂的处理延迟
        await Task.Delay(10, cancellationToken);

        // 通过分配函数获取格口号
        var chuteId = _chuteAssignmentFunc(parcelId);

        // 触发格口分配事件
        var eventArgs = new ChuteAssignmentEventArgs
        {
            ParcelId = parcelId,
            ChuteId = chuteId,
            AssignedAt = _systemClock.LocalNowOffset
        };

        _logger?.LogDebug("内存模拟客户端：推送格口分配 {ParcelId} -> 格口 {ChuteId}", parcelId, chuteId);
        ChuteAssigned?.Invoke(this, eventArgs);

        return true;
    }

    /// <summary>
    /// 通知上游系统包裹已完成落格（模拟实现）
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 新增方法
    /// </remarks>
    public Task<bool> NotifySortingCompletedAsync(
        SortingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryRuleEngineClient));
        }

        if (!_isConnected)
        {
            _logger?.LogWarning("内存模拟客户端：未连接，无法发送落格完成通知");
            return Task.FromResult(false);
        }

        _logger?.LogDebug(
            "内存模拟客户端：落格完成通知 ParcelId={ParcelId}, ChuteId={ChuteId}, IsSuccess={IsSuccess}",
            notification.ParcelId,
            notification.ActualChuteId,
            notification.IsSuccess);

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
        _logger?.LogInformation("内存模拟客户端：已释放资源");
    }
}
