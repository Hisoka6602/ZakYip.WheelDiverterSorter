using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using ZakYip.WheelDiverterSorter.Core.LineModel;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 带并发控制的摆轮路径执行器
/// </summary>
/// <remarks>
/// 包装现有执行器，添加以下功能：
/// 1. 摆轮资源锁，防止多个包裹同时控制同一摆轮
/// 2. 并发限流，限制同时处理的包裹数量
/// </remarks>
public class ConcurrentSwitchingPathExecutor : ISwitchingPathExecutor
{
    private readonly ISwitchingPathExecutor _innerExecutor;
    private readonly IDiverterResourceLockManager _lockManager;
    private readonly SemaphoreSlim _concurrencyThrottle;
    private readonly ILogger<ConcurrentSwitchingPathExecutor> _logger;
    private readonly ConcurrencyOptions _options;
    private static readonly Regex LogSanitizer = new Regex(@"[\r\n]", RegexOptions.Compiled);

    /// <summary>
    /// 清理日志字符串，防止日志注入
    /// </summary>
    private static string SanitizeForLog(int input)
    {
        return input.ToString();
    }

    /// <summary>
    /// 清理日志字符串，防止日志注入
    /// </summary>
    private static string SanitizeForLog(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        return LogSanitizer.Replace(input, "");
    }

    /// <summary>
    /// 初始化带并发控制的路径执行器
    /// </summary>
    /// <param name="innerExecutor">内部执行器（实际执行逻辑）</param>
    /// <param name="lockManager">摆轮资源锁管理器</param>
    /// <param name="options">并发控制选项</param>
    /// <param name="logger">日志记录器</param>
    public ConcurrentSwitchingPathExecutor(
        ISwitchingPathExecutor innerExecutor,
        IDiverterResourceLockManager lockManager,
        IOptions<ConcurrencyOptions> options,
        ILogger<ConcurrentSwitchingPathExecutor> logger)
    {
        _innerExecutor = innerExecutor ?? throw new ArgumentNullException(nameof(innerExecutor));
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _concurrencyThrottle = new SemaphoreSlim(
            _options.MaxConcurrentParcels,
            _options.MaxConcurrentParcels);

        _logger.LogInformation(
            "初始化并发控制执行器，最大并发数: {MaxConcurrent}",
            _options.MaxConcurrentParcels);
    }

    /// <inheritdoc/>
    public async Task<PathExecutionResult> ExecuteAsync(
        SwitchingPath path,
        CancellationToken cancellationToken = default)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // 第一层：并发限流
        await _concurrencyThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _logger.LogDebug(
                "获取并发槽位成功，目标格口: {TargetChuteId}",
                SanitizeForLog(path.TargetChuteId));

            // 第二层：按顺序获取每个摆轮的锁
            var lockHandles = new List<IDisposable>();

            try
            {
                // 为路径中的每个摆轮获取写锁
                foreach (var segment in path.Segments)
                {
                    var diverterLock = _lockManager.GetLock(segment.DiverterId);

                    // 使用超时机制获取锁
                    using var lockCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    lockCts.CancelAfter(TimeSpan.FromMilliseconds(_options.DiverterLockTimeoutMs));

                    try
                    {
                        var lockHandle = await diverterLock.AcquireWriteLockAsync(lockCts.Token)
                            .ConfigureAwait(false);
                        lockHandles.Add(lockHandle);

                        _logger.LogDebug(
                            "获取摆轮 {DiverterId} 的写锁成功",
                            segment.DiverterId);
                    }
                    catch (OperationCanceledException) when (lockCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        // 获取锁超时
                        _logger.LogWarning(
                            "获取摆轮 {DiverterId} 的锁超时（{TimeoutMs}ms）",
                            segment.DiverterId,
                            _options.DiverterLockTimeoutMs);

                        return new PathExecutionResult
                        {
                            IsSuccess = false,
                            ActualChuteId = path.FallbackChuteId,
                            FailureReason = $"获取摆轮 {segment.DiverterId} 的锁超时",
                            FailedSegment = segment,
                            FailureTime = DateTimeOffset.UtcNow
                        };
                    }
                }

                _logger.LogDebug(
                    "已获取路径所有摆轮的锁，开始执行路径，目标格口: {TargetChuteId}",
                    SanitizeForLog(path.TargetChuteId));

                // 执行实际的路径
                var result = await _innerExecutor.ExecuteAsync(path, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "路径执行完成，结果: {Success}，实际格口: {ActualChuteId}",
                    result.IsSuccess,
                    result.ActualChuteId);

                return result;
            }
            finally
            {
                // 释放所有锁（逆序释放）
                for (int i = lockHandles.Count - 1; i >= 0; i--)
                {
                    lockHandles[i]?.Dispose();
                }

                if (lockHandles.Count > 0)
                {
                    _logger.LogDebug("已释放所有摆轮锁");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("路径执行被取消");
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = "操作被取消",
                FailureTime = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "路径执行发生异常");
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = $"执行异常: {ex.Message}",
                FailureTime = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            // 释放并发槽位
            _concurrencyThrottle.Release();
            _logger.LogDebug("释放并发槽位");
        }
    }
}
