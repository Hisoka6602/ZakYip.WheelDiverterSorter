using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using CoreConfig = ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 通信配置服务接口（Application层扩展）
/// </summary>
/// <remarks>
/// 负责通信配置的业务逻辑，包括查询、更新、验证、热更新等操作。
/// 继承 Core 层的 ICommunicationConfigService 接口。
/// </remarks>
public interface ICommunicationConfigService : CoreConfig.ICommunicationConfigService
{
    /// <summary>
    /// 获取当前通信配置（Application层扩展名称）
    /// </summary>
    /// <returns>通信配置</returns>
    CommunicationConfiguration GetConfiguration();

    /// <summary>
    /// 更新通信配置
    /// </summary>
    /// <param name="command">更新命令</param>
    /// <returns>更新结果</returns>
    Task<CommunicationConfigUpdateResult> UpdateConfigurationAsync(UpdateCommunicationConfigCommand command);

    /// <summary>
    /// 重置通信配置为默认值
    /// </summary>
    /// <returns>重置后的配置</returns>
    CommunicationConfiguration ResetConfiguration();

    /// <summary>
    /// 测试连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>测试结果</returns>
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取通信状态
    /// </summary>
    /// <returns>通信状态</returns>
    CommunicationStatus GetStatus();

    /// <summary>
    /// 重置通信统计
    /// </summary>
    void ResetStatistics();
}

/// <summary>
/// 通信配置更新命令
/// </summary>
public record UpdateCommunicationConfigCommand
{
    /// <summary>
    /// 通信模式
    /// </summary>
    public CommunicationMode Mode { get; init; }

    /// <summary>
    /// 连接模式
    /// </summary>
    public ConnectionMode ConnectionMode { get; init; }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; init; } = true;

    /// <summary>
    /// TCP 配置
    /// </summary>
    public TcpConfigCommand? Tcp { get; init; }

    /// <summary>
    /// MQTT 配置
    /// </summary>
    public MqttConfigCommand? Mqtt { get; init; }

    /// <summary>
    /// SignalR 配置
    /// </summary>
    public SignalRConfigCommand? SignalR { get; init; }
}

/// <summary>
/// TCP 配置命令
/// </summary>
public record TcpConfigCommand
{
    /// <summary>
    /// TCP 服务器地址
    /// </summary>
    public string? TcpServer { get; init; }

    /// <summary>
    /// 接收缓冲区大小
    /// </summary>
    public int ReceiveBufferSize { get; init; } = 8192;

    /// <summary>
    /// 发送缓冲区大小
    /// </summary>
    public int SendBufferSize { get; init; } = 8192;

    /// <summary>
    /// 禁用 Nagle 算法
    /// </summary>
    public bool NoDelay { get; init; } = true;
}

/// <summary>
/// MQTT 配置命令
/// </summary>
public record MqttConfigCommand
{
    /// <summary>
    /// MQTT Broker 地址
    /// </summary>
    public string? MqttBroker { get; init; }

    /// <summary>
    /// MQTT 主题
    /// </summary>
    public string? MqttTopic { get; init; }

    /// <summary>
    /// QoS 等级
    /// </summary>
    public int QualityOfServiceLevel { get; init; } = 1;

    /// <summary>
    /// 干净会话
    /// </summary>
    public bool CleanSession { get; init; } = true;

    /// <summary>
    /// 会话过期时间
    /// </summary>
    public int SessionExpiryInterval { get; init; } = 3600;

    /// <summary>
    /// 消息过期时间
    /// </summary>
    public int MessageExpiryInterval { get; init; } = 0;

    /// <summary>
    /// 客户端 ID 前缀
    /// </summary>
    public string ClientIdPrefix { get; init; } = "WheelDiverter";
}

/// <summary>
/// SignalR 配置命令
/// </summary>
public record SignalRConfigCommand
{
    /// <summary>
    /// SignalR Hub 地址
    /// </summary>
    public string? SignalRHub { get; init; }

    /// <summary>
    /// 握手超时
    /// </summary>
    public int HandshakeTimeout { get; init; } = 15;

    /// <summary>
    /// Keep-Alive 间隔
    /// </summary>
    public int KeepAliveInterval { get; init; } = 30;

    /// <summary>
    /// 服务器超时
    /// </summary>
    public int ServerTimeout { get; init; } = 60;

    /// <summary>
    /// 跳过协商
    /// </summary>
    public bool SkipNegotiation { get; init; } = false;
}

/// <summary>
/// 通信配置更新结果
/// </summary>
public record CommunicationConfigUpdateResult(
    bool Success,
    string? ErrorMessage,
    CommunicationConfiguration? UpdatedConfig);

/// <summary>
/// 连接测试结果
/// </summary>
public record ConnectionTestResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; init; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 错误详情
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// 测试时间
    /// </summary>
    public DateTimeOffset TestedAt { get; init; }
}

/// <summary>
/// 通信状态
/// </summary>
public record CommunicationStatus
{
    /// <summary>
    /// 通信模式
    /// </summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// 已发送消息数
    /// </summary>
    public long MessagesSent { get; init; }

    /// <summary>
    /// 已接收消息数
    /// </summary>
    public long MessagesReceived { get; init; }

    /// <summary>
    /// 连接持续时间（秒）
    /// </summary>
    public long? ConnectionDurationSeconds { get; init; }

    /// <summary>
    /// 最后连接时间
    /// </summary>
    public DateTimeOffset? LastConnectedAt { get; init; }

    /// <summary>
    /// 最后断开时间
    /// </summary>
    public DateTimeOffset? LastDisconnectedAt { get; init; }

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string? ServerAddress { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }
}
