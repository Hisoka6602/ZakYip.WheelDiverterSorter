using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Abstractions;

/// <summary>
/// 摆轮驱动管理器接口
/// </summary>
/// <remarks>
/// 提供摆轮驱动器的热更新管理能力：
/// - 在配置更新时重新创建驱动器实例
/// - 断开旧连接并建立新连接
/// - 管理驱动器的生命周期
/// </remarks>
public interface IWheelDiverterDriverManager
{
    /// <summary>
    /// 获取当前所有活动的摆轮驱动器
    /// </summary>
    IReadOnlyDictionary<string, IWheelDiverterDriver> GetActiveDrivers();

    /// <summary>
    /// 根据摆轮ID获取驱动器
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    /// <returns>摆轮驱动器，如果不存在则返回null</returns>
    IWheelDiverterDriver? GetDriver(string diverterId);

    /// <summary>
    /// 应用新的摆轮配置（热更新）
    /// </summary>
    /// <remarks>
    /// 此方法会：
    /// 1. 断开所有现有驱动器的连接
    /// 2. 根据新配置创建并连接新的驱动器
    /// 3. 更新内部驱动器列表
    /// </remarks>
    /// <param name="configuration">新的摆轮配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>应用配置的结果</returns>
    Task<WheelDiverterConfigApplyResult> ApplyConfigurationAsync(
        WheelDiverterConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开所有驱动器连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 重新连接所有驱动器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重连结果</returns>
    Task<WheelDiverterReconnectResult> ReconnectAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 摆轮配置应用结果
/// </summary>
public record class WheelDiverterConfigApplyResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 成功连接的驱动器数量
    /// </summary>
    public required int ConnectedCount { get; init; }

    /// <summary>
    /// 配置的驱动器总数
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// 连接失败的驱动器ID列表
    /// </summary>
    public required IReadOnlyList<string> FailedDriverIds { get; init; }

    /// <summary>
    /// 错误消息（如果有）
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 摆轮驱动器重连结果
/// </summary>
public record class WheelDiverterReconnectResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 成功重连的驱动器数量
    /// </summary>
    public required int ReconnectedCount { get; init; }

    /// <summary>
    /// 驱动器总数
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// 重连失败的驱动器ID列表
    /// </summary>
    public required IReadOnlyList<string> FailedDriverIds { get; init; }
}
