using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 统一的摆轮命令执行器实现
/// </summary>
/// <remarks>
/// <para>此类实现了统一的"发送命令 + 等待反馈"流程，包括：</para>
/// <list type="bullet">
/// <item>通过 <see cref="IWheelDiverterDriverManager"/> 获取驱动器</item>
/// <item>执行摆轮动作（左转、右转、直通）</item>
/// <item>统一处理超时、错误码、异常</item>
/// <item>统一记录日志（成功/失败/超时）</item>
/// </list>
/// <para>仿真模式与真实模式使用同一个执行器，区别只在于注入的驱动实现。</para>
/// <para>
/// 所有 try/catch + 日志逻辑都集中在此类内部，上层调用方只关心
/// <see cref="OperationResult"/> 的业务含义。
/// </para>
/// </remarks>
public sealed class WheelCommandExecutor : IWheelCommandExecutor
{
    private readonly IWheelDiverterDriverManager _driverManager;
    private readonly ILogger<WheelCommandExecutor> _logger;
    private static readonly Regex LogSanitizer = new(@"[\r\n]", RegexOptions.Compiled);

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
    /// 初始化摆轮命令执行器
    /// </summary>
    /// <param name="driverManager">摆轮驱动管理器</param>
    /// <param name="logger">日志记录器</param>
    public WheelCommandExecutor(
        IWheelDiverterDriverManager driverManager,
        ILogger<WheelCommandExecutor> logger)
    {
        _driverManager = driverManager ?? throw new ArgumentNullException(nameof(driverManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<OperationResult> ExecuteAsync(
        WheelCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var diverterId = command.DiverterId;
        var direction = command.Direction;
        var timeout = command.Timeout;

        _logger.LogDebug(
            "[摆轮命令执行] 开始执行 | 摆轮={DiverterId} | 方向={Direction} | 超时={TimeoutMs}ms | 序号={Seq}",
            SanitizeForLog(diverterId),
            direction,
            timeout.TotalMilliseconds,
            command.SequenceNumber);

        try
        {
            // 1. 获取摆轮驱动器
            var driver = _driverManager.GetDriver(diverterId);
            if (driver == null)
            {
                _logger.LogError(
                    "[摆轮命令执行] 摆轮未找到 | 摆轮={DiverterId}",
                    SanitizeForLog(diverterId));

                return OperationResult.Failure(
                    ErrorCodes.WheelNotFound,
                    $"找不到摆轮控制器: {diverterId}");
            }

            // 2. 创建带超时的取消令牌
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // 3. 执行摆轮动作
            bool success;
            try
            {
                success = direction switch
                {
                    DiverterDirection.Left => await driver.TurnLeftAsync(cts.Token),
                    DiverterDirection.Right => await driver.TurnRightAsync(cts.Token),
                    DiverterDirection.Straight => await driver.PassThroughAsync(cts.Token),
                    _ => throw new ArgumentException($"不支持的摆轮方向: {direction}", nameof(command))
                };
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // 超时（内部超时触发，非外部取消）
                _logger.LogWarning(
                    "[摆轮命令执行] 命令超时 | 摆轮={DiverterId} | 方向={Direction} | 超时={TimeoutMs}ms",
                    SanitizeForLog(diverterId),
                    direction,
                    timeout.TotalMilliseconds);

                return OperationResult.Failure(
                    ErrorCodes.WheelCommandTimeout,
                    $"摆轮 {diverterId} 命令执行超时（{timeout.TotalMilliseconds}ms）");
            }

            // 4. 处理执行结果
            if (success)
            {
                _logger.LogInformation(
                    "[摆轮命令执行] 执行成功 | 摆轮={DiverterId} | 方向={Direction}",
                    SanitizeForLog(diverterId),
                    direction);

                return OperationResult.Success();
            }
            else
            {
                _logger.LogWarning(
                    "[摆轮命令执行] 执行失败 | 摆轮={DiverterId} | 方向={Direction}",
                    SanitizeForLog(diverterId),
                    direction);

                return OperationResult.Failure(
                    ErrorCodes.WheelCommandFailed,
                    $"摆轮 {diverterId} 命令执行失败");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 外部取消
            _logger.LogWarning(
                "[摆轮命令执行] 操作被取消 | 摆轮={DiverterId}",
                SanitizeForLog(diverterId));

            throw; // 取消异常向上传递
        }
        catch (WheelDriverException ex)
        {
            // 驱动层异常统一转换为 OperationResult
            _logger.LogError(
                ex,
                "[摆轮命令执行] 驱动异常 | 摆轮={DiverterId} | 错误码={ErrorCode}",
                SanitizeForLog(diverterId),
                ex.ErrorCode);

            return ex.ToOperationResult();
        }
        catch (ArgumentException ex)
        {
            // 参数异常（如不支持的方向）
            _logger.LogError(
                ex,
                "[摆轮命令执行] 参数错误 | 摆轮={DiverterId}",
                SanitizeForLog(diverterId));

            return OperationResult.Failure(
                ErrorCodes.WheelInvalidDirection,
                ex.Message);
        }
        catch (Exception ex)
        {
            // 其他未预期异常
            _logger.LogError(
                ex,
                "[摆轮命令执行] 未知异常 | 摆轮={DiverterId}",
                SanitizeForLog(diverterId));

            return OperationResult.Failure(
                ErrorCodes.WheelCommunicationError,
                $"摆轮 {diverterId} 通信错误: {ex.Message}");
        }
    }
}
