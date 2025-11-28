using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

/// <summary>
/// 摆轮命令执行器接口
/// </summary>
/// <remarks>
/// 本接口属于 Core 层，定义摆轮命令执行的抽象契约。
/// 
/// <para>统一所有"发送摆轮命令并等待执行结果"的行为，包括：</para>
/// <list type="bullet">
/// <item>使用驱动接口发送命令</item>
/// <item>等待反馈/状态</item>
/// <item>处理超时、错误码、异常</item>
/// <item>记录日志（成功/失败等）</item>
/// </list>
/// <para>仿真模式与真实模式使用同一个执行器，区别只在于注入的驱动实现。</para>
/// <para>
/// 所有异常处理和日志记录都在执行器内部完成，上层调用方只需关心
/// <see cref="OperationResult"/> 的业务含义。
/// </para>
/// </remarks>
public interface IWheelCommandExecutor
{
    /// <summary>
    /// 异步执行摆轮命令
    /// </summary>
    /// <param name="command">摆轮命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>
    /// 返回 <see cref="OperationResult"/>：
    /// <list type="bullet">
    /// <item>成功时 IsSuccess = true</item>
    /// <item>超时时返回错误码 <see cref="ErrorCodes.WheelCommandTimeout"/></item>
    /// <item>驱动失败时返回错误码 <see cref="ErrorCodes.WheelCommandFailed"/></item>
    /// <item>通信错误时返回错误码 <see cref="ErrorCodes.WheelCommunicationError"/></item>
    /// <item>摆轮未找到时返回错误码 <see cref="ErrorCodes.WheelNotFound"/></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>此方法保证不会抛出异常（除非 cancellationToken 被触发）。</para>
    /// <para>所有异常都会被捕获并转换为相应的 <see cref="OperationResult"/> 失败结果。</para>
    /// </remarks>
    Task<OperationResult> ExecuteAsync(
        WheelCommand command,
        CancellationToken cancellationToken = default);
}
