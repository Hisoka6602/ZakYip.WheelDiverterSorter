namespace ZakYip.WheelDiverterSorter.Core.Hardware.Devices;

/// <summary>
/// 摆轮操作结果
/// </summary>
/// <remarks>
/// 封装摆轮操作的执行结果，包含诊断信息用于调试和监控。
/// </remarks>
public record class DiverterOperationResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 连接信息（例如：IP:端口）
    /// </summary>
    /// <example>192.168.0.200:200</example>
    public string? ConnectionInfo { get; init; }

    /// <summary>
    /// 发送的命令（十六进制格式）
    /// </summary>
    /// <example>51 52 57 51 52 51 FE</example>
    public string? CommandSent { get; init; }

    /// <summary>
    /// 错误消息（操作失败时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static DiverterOperationResult Success(string? connectionInfo = null, string? commandSent = null)
    {
        return new DiverterOperationResult
        {
            IsSuccess = true,
            ConnectionInfo = connectionInfo,
            CommandSent = commandSent
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static DiverterOperationResult Failure(string? errorMessage = null, string? connectionInfo = null, string? commandSent = null)
    {
        return new DiverterOperationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ConnectionInfo = connectionInfo,
            CommandSent = commandSent
        };
    }
}
