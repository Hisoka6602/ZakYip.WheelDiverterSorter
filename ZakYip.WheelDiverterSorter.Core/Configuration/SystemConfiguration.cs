using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 系统配置模型
/// </summary>
/// <remarks>
/// 存储系统级别的配置参数，支持热重载和环境迁移
/// </remarks>
public class SystemConfiguration
{
    /// <summary>
    /// 配置ID（LiteDB自动生成）
    /// </summary>
    [BsonId]
    public int Id { get; set; }

    /// <summary>
    /// 配置名称（唯一标识符）
    /// </summary>
    /// <remarks>
    /// 使用固定值 "system" 确保只有一条系统配置记录
    /// </remarks>
    public string ConfigName { get; set; } = "system";

    /// <summary>
    /// 异常格口ID（数字ID，与路由配置中的格口ID对应）
    /// </summary>
    /// <remarks>
    /// <para>当包裹分拣失败或无法分配格口时使用的目标格口</para>
    /// <para>异常格口永远不能为空。如果未配置，系统将使用默认值</para>
    /// <para>建议：配置为在最末端一个摆轮的直行方向的格口，确保包裹能够安全通过系统</para>
    /// </remarks>
    public int ExceptionChuteId { get; set; } = 999;

    /// <summary>
    /// MQTT默认端口
    /// </summary>
    /// <remarks>
    /// 当MQTT Broker地址中未指定端口时使用的默认端口
    /// </remarks>
    public int MqttDefaultPort { get; set; } = 1883;

    /// <summary>
    /// TCP默认端口
    /// </summary>
    /// <remarks>
    /// 当TCP服务器地址中未指定端口时使用的默认端口
    /// </remarks>
    public int TcpDefaultPort { get; set; } = 8888;

    /// <summary>
    /// 格口分配超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 等待RuleEngine推送格口分配的最大时间，超时后使用异常格口
    /// </remarks>
    public int ChuteAssignmentTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 通用的请求超时时间，用于HTTP、TCP等协议
    /// </remarks>
    public int RequestTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    /// <remarks>
    /// 请求失败时的重试次数
    /// </remarks>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 每次重试之间的延迟时间
    /// </remarks>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    /// <remarks>
    /// 连接断开时是否自动尝试重连
    /// </remarks>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 雷赛控制面板 IO 模块配置
    /// </summary>
    /// <remarks>
    /// 用于配置电柜面板上的物理按键和指示灯，包括急停、启动、停止、复位按钮以及三色灯等
    /// </remarks>
    public LeadshineCabinetIoOptions LeadshineCabinetIo { get; set; } = new();

    /// <summary>
    /// IO 联动配置
    /// </summary>
    /// <remarks>
    /// 用于配置系统在不同状态（运行/停止）下需要联动控制的 IO 端口
    /// </remarks>
    public IoLinkageOptions IoLinkage { get; set; } = new();

    /// <summary>
    /// 配置版本号
    /// </summary>
    /// <remarks>
    /// 用于跟踪配置变更历史
    /// </remarks>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间（UTC）
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 验证配置参数的有效性
    /// </summary>
    /// <returns>验证结果和错误消息</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (ExceptionChuteId <= 0)
        {
            return (false, "异常格口ID必须大于0");
        }

        if (MqttDefaultPort < 1 || MqttDefaultPort > 65535)
        {
            return (false, "MQTT默认端口必须在1-65535之间");
        }

        if (TcpDefaultPort < 1 || TcpDefaultPort > 65535)
        {
            return (false, "TCP默认端口必须在1-65535之间");
        }

        if (ChuteAssignmentTimeoutMs < 1000 || ChuteAssignmentTimeoutMs > 60000)
        {
            return (false, "格口分配超时时间必须在1000-60000毫秒之间");
        }

        if (RequestTimeoutMs < 1000 || RequestTimeoutMs > 60000)
        {
            return (false, "请求超时时间必须在1000-60000毫秒之间");
        }

        if (RetryCount < 0 || RetryCount > 10)
        {
            return (false, "重试次数必须在0-10之间");
        }

        if (RetryDelayMs < 100 || RetryDelayMs > 10000)
        {
            return (false, "重试延迟必须在100-10000毫秒之间");
        }

        return (true, null);
    }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static SystemConfiguration GetDefault()
    {
        return new SystemConfiguration
        {
            ConfigName = "system",
            ExceptionChuteId = 999,
            MqttDefaultPort = 1883,
            TcpDefaultPort = 8888,
            ChuteAssignmentTimeoutMs = 10000,
            RequestTimeoutMs = 5000,
            RetryCount = 3,
            RetryDelayMs = 1000,
            EnableAutoReconnect = true,
            LeadshineCabinetIo = new LeadshineCabinetIoOptions(),
            IoLinkage = new IoLinkageOptions(),
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
