using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// 上游路由客户端基类，提供公共的连接管理、重试和日志记录功能
/// </summary>
/// <remarks>
/// PR-UPSTREAM-UNIFIED: 彻底重构为1事件+2方法模式，删除所有旧方法。
/// 连接管理（包括自动重连）由子类内部实现，对外透明。
/// </remarks>
public abstract class RuleEngineClientBase : IUpstreamRoutingClient, IDisposable
{
    protected readonly ILogger Logger;
    protected readonly UpstreamConnectionOptions Options;
    protected readonly ISystemClock SystemClock;
    private bool _disposed;
    private Action? _onMessageSent;
    private Action? _onMessageReceived;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public abstract bool IsConnected { get; }

    /// <summary>
    /// 格口分配事件
    /// </summary>
    public event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    protected RuleEngineClientBase(
        ILogger logger, 
        UpstreamConnectionOptions options, 
        ISystemClock systemClock)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        SystemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <summary>
    /// 设置消息统计回调
    /// </summary>
    /// <param name="onMessageSent">消息发送回调</param>
    /// <param name="onMessageReceived">消息接收回调</param>
    public void SetStatsCallbacks(Action? onMessageSent, Action? onMessageReceived)
    {
        _onMessageSent = onMessageSent;
        _onMessageReceived = onMessageReceived;
    }

    /// <summary>
    /// 发送消息到上游系统（统一发送接口）
    /// </summary>
    /// <param name="message">上游消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功发送</returns>
    /// <remarks>
    /// 子类必须实现具体的发送逻辑。
    /// 连接管理（包括自动重连）应在子类内部处理。
    /// </remarks>
    public abstract Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ping上游系统进行健康检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否Ping成功</returns>
    /// <remarks>
    /// 默认实现：返回当前连接状态。子类可以override实现更复杂的健康检查逻辑。
    /// </remarks>
    public virtual Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult(IsConnected);
    }

    /// <summary>
    /// 热更新连接参数
    /// </summary>
    /// <param name="options">新的连接选项</param>
    /// <remarks>
    /// 默认实现：记录警告日志。子类应override实现具体的热更新逻辑。
    /// </remarks>
    public virtual Task UpdateOptionsAsync(UpstreamConnectionOptions options)
    {
        ThrowIfDisposed();
        Logger.LogWarning("当前实现不支持热更新连接参数，请在子类中override此方法。Options={Options}", options);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 触发格口分配事件
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="chuteId">格口ID</param>
    /// <param name="assignedAt">分配时间</param>
    /// <param name="dwsPayload">DWS数据（可选）</param>
    /// <param name="metadata">元数据（可选）</param>
    /// <remarks>
    /// PR-UPSTREAM02: 更新参数以支持 DWS 数据，将 notificationTime 重命名为 assignedAt
    /// </remarks>
    protected void OnChuteAssignmentReceived(
        long parcelId, 
        long chuteId, 
        DateTimeOffset assignedAt, 
        DwsMeasurement? dwsPayload = null,
        Dictionary<string, string>? metadata = null)
    {
        // 记录接收消息统计
        _onMessageReceived?.Invoke();
        
        var notification = new ChuteAssignmentEventArgs
        {
            ParcelId = parcelId,
            ChuteId = chuteId,
            AssignedAt = assignedAt,
            DwsPayload = dwsPayload,
            Metadata = metadata
        };
        ChuteAssigned.SafeInvoke(this, notification, Logger, nameof(ChuteAssigned));
    }

    /// <summary>
    /// 记录消息发送统计
    /// </summary>
    /// <param name="success">是否成功发送</param>
    protected void RecordMessageSent(bool success)
    {
        if (success)
        {
            _onMessageSent?.Invoke();
        }
    }

    /// <summary>
    /// 将事件中的 DWS 数据转换为 Core 层的 DwsMeasurement
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 提取为公共方法以减少重复代码
    /// </remarks>
    protected static DwsMeasurement? MapDwsPayload(DwsMeasurementEventArgs? eventArgs)
    {
        if (eventArgs == null)
        {
            return null;
        }

        return new DwsMeasurement
        {
            WeightGrams = eventArgs.WeightGrams,
            LengthMm = eventArgs.LengthMm,
            WidthMm = eventArgs.WidthMm,
            HeightMm = eventArgs.HeightMm,
            VolumetricWeightGrams = eventArgs.VolumetricWeightGrams,
            Barcode = eventArgs.Barcode,
            MeasuredAt = eventArgs.MeasuredAt
        };
    }

    /// <summary>
    /// 执行带重试的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">操作函数</param>
    /// <param name="operationName">操作名称（用于日志）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    protected async Task<T?> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= Options.RetryCount)
        {
            try
            {
                Logger.LogDebug("{Operation}（第{Retry}次尝试）", operationName, retryCount + 1);
                return await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Logger.LogWarning(ex, "{Operation}失败（第{Retry}次尝试）", operationName, retryCount + 1);

                retryCount++;
                if (retryCount <= Options.RetryCount)
                {
                    await Task.Delay(Options.RetryDelayMs, cancellationToken);
                }
            }
        }

        Logger.LogError(lastException, "{Operation}失败，已达到最大重试次数", operationName);
        return default;
    }

    /// <summary>
    /// 执行带重试的布尔操作
    /// </summary>
    /// <param name="operation">操作函数</param>
    /// <param name="operationName">操作名称（用于日志）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    protected async Task<bool> ExecuteWithRetryAsync(
        Func<CancellationToken, Task<bool>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteWithRetryAsync<bool?>(
            async ct => await operation(ct),
            operationName,
            cancellationToken);
        return result ?? false;
    }

    /// <summary>
    /// 确保已连接（子类实现自动重连逻辑）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否已连接</returns>
    /// <remarks>
    /// PR-UPSTREAM-UNIFIED: 移除ConnectAsync调用，连接管理由子类内部实现
    /// </remarks>
    protected Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsConnected);
    }

    /// <summary>
    /// 验证包裹ID是否有效
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <exception cref="ArgumentException">包裹ID无效时抛出</exception>
    protected static void ValidateParcelId(long parcelId)
    {
        if (parcelId <= 0)
        {
            throw new ArgumentException("包裹ID必须为正数", nameof(parcelId));
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Dispose(true);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放托管和非托管资源
    /// </summary>
    /// <param name="disposing">是否正在释放托管资源</param>
    /// <remarks>
    /// PR-UPSTREAM-UNIFIED: 移除DisconnectAsync调用，连接管理由子类内部实现
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
        // 子类应在其Dispose实现中处理连接关闭
    }

    /// <summary>
    /// 检查对象是否已释放
    /// </summary>
    /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

}
