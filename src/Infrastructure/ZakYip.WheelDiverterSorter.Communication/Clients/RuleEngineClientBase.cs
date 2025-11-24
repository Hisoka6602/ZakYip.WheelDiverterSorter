using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// RuleEngine客户端基类，提供公共的连接管理、重试和日志记录功能
/// </summary>
public abstract class RuleEngineClientBase : IRuleEngineClient
{
    protected readonly ILogger Logger;
    protected readonly RuleEngineConnectionOptions Options;
    protected readonly ISystemClock SystemClock;
    private bool _disposed;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public abstract bool IsConnected { get; }

    /// <summary>
    /// 格口分配通知事件
    /// </summary>
    public event EventHandler<ChuteAssignmentNotificationEventArgs>? ChuteAssignmentReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    protected RuleEngineClientBase(ILogger logger, RuleEngineConnectionOptions options, ISystemClock systemClock)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        SystemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    /// <summary>
    /// 连接到RuleEngine
    /// </summary>
    public abstract Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与RuleEngine的连接
    /// </summary>
    public abstract Task DisconnectAsync();

    /// <summary>
    /// 通知RuleEngine包裹已到达
    /// </summary>
    public abstract Task<bool> NotifyParcelDetectedAsync(long parcelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 触发格口分配接收事件
    /// </summary>
    /// <param name="notification">格口分配通知</param>
    protected void OnChuteAssignmentReceived(ChuteAssignmentNotificationEventArgs notification)
    {
        ChuteAssignmentReceived?.Invoke(this, notification);
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
    /// 确保已连接，如果未连接则尝试连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否已连接</returns>
    protected async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return true;
        }

        return await ConnectAsync(cancellationToken);
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
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
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
