using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using ZakYip.WheelDiverterSorter.Core.LineModel;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
namespace ZakYip.WheelDiverterSorter.Execution.Concurrency;

/// <summary>
/// 带并发控制的摆轮路径执行器
/// </summary>
/// <remarks>
/// 包装现有执行器，添加并发限流功能，限制同时处理的包裹数量
/// </remarks>
public class ConcurrentSwitchingPathExecutor : ISwitchingPathExecutor
{
    private readonly ISwitchingPathExecutor _innerExecutor;
    private readonly SemaphoreSlim _concurrencyThrottle;
    private readonly ILogger<ConcurrentSwitchingPathExecutor> _logger;
    private readonly ConcurrencyOptions _options;
    private readonly ISystemClock _clock;
    private static readonly Regex LogSanitizer = new Regex(@"[\r\n]", RegexOptions.Compiled);

    /// <summary>
    /// 清理日志字符串，防止日志注入
    /// </summary>
    private static string SanitizeForLog(long input)
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
    /// <param name="options">并发控制选项</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="clock">系统时钟</param>
    public ConcurrentSwitchingPathExecutor(
        ISwitchingPathExecutor innerExecutor,
        IOptions<ConcurrencyOptions> options,
        ILogger<ConcurrentSwitchingPathExecutor> logger,
        ISystemClock clock)
    {
        _innerExecutor = innerExecutor ?? throw new ArgumentNullException(nameof(innerExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        _concurrencyThrottle = new SemaphoreSlim(
            _options.MaxConcurrentParcels,
            _options.MaxConcurrentParcels);

        _logger.LogInformation(
            "初始化并发控制执行器，最大并发数: {MaxConcurrent}",
            _options.MaxConcurrentParcels);
    }

    /// <inheritdoc/>
    public async ValueTask<PathExecutionResult> ExecuteAsync(
        SwitchingPath path,
        CancellationToken cancellationToken = default)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // 并发限流
        await _concurrencyThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // 执行实际的路径
            var result = await _innerExecutor.ExecuteAsync(path, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "路径执行完成，结果: {Success}，实际格口: {ActualChuteId}",
                result.IsSuccess,
                result.ActualChuteId);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("路径执行被取消");
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = "操作被取消",
                FailureTime = _clock.LocalNowOffset
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
                FailureTime = _clock.LocalNowOffset
            };
        }
        finally
        {
            // 释放并发槽位
            _concurrencyThrottle.Release();
        }
    }
}
