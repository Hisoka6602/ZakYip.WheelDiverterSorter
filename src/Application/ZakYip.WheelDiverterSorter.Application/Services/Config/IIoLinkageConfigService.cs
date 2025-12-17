using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// IO 联动配置服务接口
/// </summary>
/// <remarks>
/// 负责 IO 联动配置的业务逻辑，包括查询、更新、触发联动等操作
/// </remarks>
public interface IIoLinkageConfigService
{
    /// <summary>
    /// 获取当前 IO 联动配置
    /// </summary>
    /// <returns>IO 联动配置</returns>
    IoLinkageConfiguration GetConfiguration();

    /// <summary>
    /// 更新 IO 联动配置
    /// </summary>
    /// <param name="command">更新命令</param>
    /// <returns>更新结果</returns>
    IoLinkageConfigUpdateResult UpdateConfiguration(UpdateIoLinkageConfigCommand command);

    /// <summary>
    /// 手动触发 IO 联动
    /// </summary>
    /// <param name="systemState">目标系统状态</param>
    /// <returns>触发结果</returns>
    Task<IoLinkageTriggerResult> TriggerIoLinkageAsync(SystemState systemState);

    /// <summary>
    /// 获取指定 IO 点的状态
    /// </summary>
    /// <param name="bitNumber">IO 端口编号</param>
    /// <returns>IO 点状态</returns>
    Task<IoPointStatus> GetIoPointStatusAsync(int bitNumber);

    /// <summary>
    /// 批量获取 IO 点状态
    /// </summary>
    /// <param name="bitNumbers">IO 端口编号列表</param>
    /// <returns>IO 点状态列表</returns>
    Task<List<IoPointStatus>> GetBatchIoPointStatusAsync(IEnumerable<int> bitNumbers);

    /// <summary>
    /// 设置指定 IO 点的电平状态
    /// </summary>
    /// <param name="bitNumber">IO 端口编号</param>
    /// <param name="level">目标电平</param>
    /// <returns>设置结果</returns>
    Task<IoPointSetResult> SetIoPointAsync(int bitNumber, TriggerLevel level);

    /// <summary>
    /// 批量设置 IO 点
    /// </summary>
    /// <param name="ioPoints">IO 点列表</param>
    /// <returns>设置结果</returns>
    Task<BatchIoPointSetResult> SetBatchIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints);
}

/// <summary>
/// IO 联动配置更新命令
/// </summary>
public record UpdateIoLinkageConfigCommand
{
    /// <summary>
    /// 是否启用 IO 联动
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 就绪状态下的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> ReadyStateIos { get; init; } = new();

    /// <summary>
    /// 运行状态下的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> RunningStateIos { get; init; } = new();

    /// <summary>
    /// 停止状态下的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> StoppedStateIos { get; init; } = new();

    /// <summary>
    /// 急停状态下的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> EmergencyStopStateIos { get; init; } = new();

    /// <summary>
    /// 上游连接异常状态下的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> UpstreamConnectionExceptionStateIos { get; init; } = new();

    /// <summary>
    /// 摆轮异常状态下的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> DiverterExceptionStateIos { get; init; } = new();

    /// <summary>
    /// 运行前预警结束后的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> PostPreStartWarningStateIos { get; init; } = new();

    /// <summary>
    /// 摆轮断联状态下的 IO 点
    /// </summary>
    public List<IoLinkagePointCommand> WheelDiverterDisconnectedStateIos { get; init; } = new();
}

/// <summary>
/// IO 联动点命令
/// </summary>
public record IoLinkagePointCommand
{
    /// <summary>
    /// IO 端口编号
    /// </summary>
    public int BitNumber { get; init; }

    /// <summary>
    /// 电平有效性
    /// </summary>
    public TriggerLevel Level { get; init; }
}

/// <summary>
/// IO 联动配置更新结果
/// </summary>
public record IoLinkageConfigUpdateResult(
    bool Success,
    string? ErrorMessage,
    IoLinkageConfiguration? UpdatedConfig);

/// <summary>
/// IO 联动触发结果
/// </summary>
public record IoLinkageTriggerResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 系统状态
    /// </summary>
    public string SystemState { get; init; } = string.Empty;

    /// <summary>
    /// 触发的 IO 点
    /// </summary>
    public List<IoPointInfo> TriggeredIoPoints { get; init; } = new();

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// IO 点信息
/// </summary>
public record IoPointInfo
{
    /// <summary>
    /// IO 端口编号
    /// </summary>
    public int BitNumber { get; init; }

    /// <summary>
    /// 电平
    /// </summary>
    public string Level { get; init; } = string.Empty;
}

/// <summary>
/// IO 点状态
/// </summary>
public record IoPointStatus
{
    /// <summary>
    /// IO 端口编号
    /// </summary>
    public int BitNumber { get; init; }

    /// <summary>
    /// 当前状态（true=高电平，false=低电平）
    /// </summary>
    public bool State { get; init; }

    /// <summary>
    /// 电平描述
    /// </summary>
    public string Level => State ? "High" : "Low";
}

/// <summary>
/// IO 点设置结果
/// </summary>
public record IoPointSetResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// IO 端口编号
    /// </summary>
    public int BitNumber { get; init; }

    /// <summary>
    /// 设置的电平
    /// </summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 批量 IO 点设置结果
/// </summary>
public record BatchIoPointSetResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 请求的总数
    /// </summary>
    public int TotalRequested { get; init; }

    /// <summary>
    /// 有效数量
    /// </summary>
    public int ValidCount { get; init; }

    /// <summary>
    /// 跳过数量
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// 设置的 IO 点
    /// </summary>
    public List<IoPointInfo> IoPoints { get; init; } = new();

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }
}
