using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 内存模拟的规则引擎客户端
/// </summary>
/// <remarks>
/// 用于仿真和测试，不进行真实的网络通信。
/// 支持三种模式：Formal（从配置获取格口）、FixedChute（固定格口）、RoundRobin（轮询）
/// </remarks>
public class InMemoryRuleEngineClient : IRuleEngineClient
{
    private readonly ILogger<InMemoryRuleEngineClient>? _logger;
    private readonly Func<long, int> _chuteAssignmentFunc;
    private bool _isConnected;
    private bool _isDisposed;

    /// <summary>
    /// 格口分配通知事件
    /// </summary>
    public event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chuteAssignmentFunc">格口分配函数，输入包裹ID，返回格口号</param>
    /// <param name="logger">日志记录器</param>
    public InMemoryRuleEngineClient(
        Func<long, int> chuteAssignmentFunc,
        ILogger<InMemoryRuleEngineClient>? logger = null)
    {
        _chuteAssignmentFunc = chuteAssignmentFunc ?? throw new ArgumentNullException(nameof(chuteAssignmentFunc));
        _logger = logger;
    }

    /// <summary>
    /// 连接到RuleEngine（模拟实现）
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
    /// 断开与RuleEngine的连接（模拟实现）
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
    /// 通知RuleEngine包裹已到达（模拟实现）
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
        var eventArgs = new ChuteAssignmentNotificationEventArgs
        {
            ParcelId = parcelId,
            ChuteId = chuteId
        };

        _logger?.LogDebug("内存模拟客户端：推送格口分配 {ParcelId} -> 格口 {ChuteId}", parcelId, chuteId);
        ChuteAssignmentReceived?.Invoke(this, eventArgs);

        return true;
    }

    /// <summary>
    /// 请求包裹的格口号（已废弃，保留用于兼容性）
    /// </summary>
    [Obsolete("使用NotifyParcelDetectedAsync配合ChuteAssignmentReceived事件代替")]
    public Task<ChuteAssignmentResponse> RequestChuteAssignmentAsync(
        long parcelId,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryRuleEngineClient));
        }

        if (!_isConnected)
        {
            return Task.FromResult(new ChuteAssignmentResponse
            {
                ParcelId = parcelId,
                ChuteId = 0,
                IsSuccess = false,
                ErrorMessage = "客户端未连接"
            });
        }

        var chuteId = _chuteAssignmentFunc(parcelId);

        return Task.FromResult(new ChuteAssignmentResponse
        {
            ParcelId = parcelId,
            ChuteId = chuteId,
            IsSuccess = true
        });
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
