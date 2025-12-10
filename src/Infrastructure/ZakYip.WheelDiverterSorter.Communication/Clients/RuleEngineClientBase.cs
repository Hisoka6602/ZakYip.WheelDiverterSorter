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
/// PR-U1: 合并 IRuleEngineClient 到 IUpstreamRoutingClient，删除中间适配层
/// PR-UPSTREAM02: 添加 NotifySortingCompletedAsync 方法，将事件重命名为 ChuteAssigned
/// </remarks>
public abstract class RuleEngineClientBase : IUpstreamRoutingClient, IDisposable
{
    protected readonly ILogger Logger;
    protected readonly UpstreamConnectionOptions Options;
    protected readonly ISystemClock SystemClock;
    private bool _disposed;

    /// <summary>
    /// 客户端是否已连接
    /// </summary>
    public abstract bool IsConnected { get; }

    /// <summary>
    /// 格口分配事件
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 从 ChuteAssignmentReceived 重命名为 ChuteAssigned
    /// </remarks>
    public event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">连接配置</param>
    /// <param name="systemClock">系统时钟</param>
    protected RuleEngineClientBase(ILogger logger, UpstreamConnectionOptions options, ISystemClock systemClock)
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
    /// 通知RuleEngine包裹已完成落格
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 新增方法
    /// </remarks>
    public abstract Task<bool> NotifySortingCompletedAsync(SortingCompletedNotification notification, CancellationToken cancellationToken = default);

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
