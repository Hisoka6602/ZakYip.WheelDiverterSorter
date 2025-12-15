using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 上游路由通讯客户端接口
/// </summary>
/// <remarks>
/// 本接口属于 Core 层，定义与上游系统通讯的抽象契约。
/// 抽象了与上游系统（RuleEngine等）的通信，隐藏底层协议（TCP/SignalR/MQTT）细节。
/// 
/// <para><b>职责</b>：</para>
/// <list type="bullet">
///   <item>连接/断开上游系统</item>
///   <item>通知上游包裹到达（fire-and-forget，不等待格口分配）</item>
///   <item>接收上游主动推送的格口分配（通过 ChuteAssigned 事件）</item>
///   <item>通知上游包裹落格完成（fire-and-forget）</item>
/// </list>
/// 
/// <para><b>交互流程</b>（PR-UPSTREAM02）：</para>
/// <list type="number">
///   <item>入口检测时调用 NotifyParcelDetectedAsync，发送包裹检测通知（fire-and-forget）</item>
///   <item>上游匹配格口后，主动推送格口分配，触发 ChuteAssigned 事件</item>
///   <item>包裹落格后调用 NotifySortingCompletedAsync，发送落格完成通知（fire-and-forget）</item>
/// </list>
/// 
/// <para><b>实现层</b>：</para>
/// Communication 项目实现此接口。
/// PR-U1: 合并 IRuleEngineClient 语义到此接口，删除中间适配层。
/// PR-UPSTREAM-UNIFIED: 统一上游接口为1事件+2方法，删除所有旧方法实现彻底重构。
/// </remarks>
public interface IUpstreamRoutingClient : IDisposable
{
    /// <summary>
    /// 是否已连接到上游系统
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 格口分配接收事件（1个事件）
    /// </summary>
    /// <remarks>
    /// 当上游系统主动推送格口分配时触发此事件。
    /// 事件参数包含包裹ID、分配的格口ID和DWS数据。
    /// </remarks>
    event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;

    /// <summary>
    /// 发送消息到上游系统（方法1/2：统一发送接口）
    /// </summary>
    /// <param name="message">上游消息（包裹检测通知或落格完成通知）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功发送</returns>
    /// <remarks>
    /// 统一发送接口，支持：
    /// - ParcelDetectedMessage - 包裹检测通知
    /// - SortingCompletedMessage - 落格完成通知
    /// 发送即忘记模式：发送失败只记录日志，不重试。
    /// 连接管理（包括自动重连）由实现类内部处理，调用方无需关心连接状态。
    /// </remarks>
    Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ping上游系统进行健康检查（方法2/2）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否Ping成功</returns>
    /// <remarks>
    /// 用于主动健康检查，验证上游连接状态。
    /// 内部自动重连逻辑独立处理，此方法仅用于按需检查。
    /// </remarks>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 热更新连接参数
    /// </summary>
    /// <param name="options">新的连接选项</param>
    /// <remarks>
    /// 支持运行时更新连接参数，内部会断开当前连接并使用新参数重新连接。
    /// 支持Client/Server模式切换、地址/端口更新等。
    /// </remarks>
    Task UpdateOptionsAsync(UpstreamConnectionOptions options);
}

/// <summary>
/// 格口分配事件参数
/// </summary>
/// <remarks>
/// 本类型属于 Core 层，用于传递格口分配事件的数据。
/// 纯数据对象，不依赖具体的 Communication 层类型。
/// PR-UPSTREAM02: 扩展 DWS 数据支持。
/// </remarks>
public record ChuteAssignmentEventArgs
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 分配的格口ID
    /// </summary>
    public required long ChuteId { get; init; }

    /// <summary>
    /// 分配时间
    /// </summary>
    public required DateTimeOffset AssignedAt { get; init; }

    /// <summary>
    /// DWS（尺寸重量扫描）数据（可选）
    /// </summary>
    /// <remarks>
    /// PR-UPSTREAM02: 新增字段，由上游在推送格口分配时一并提供。
    /// </remarks>
    public DwsMeasurement? DwsPayload { get; init; }

    /// <summary>
    /// 额外的元数据（可选）
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// DWS（尺寸重量扫描）测量数据
/// </summary>
/// <remarks>
/// PR-UPSTREAM02: 新增值对象，封装上游推送的DWS数据。
/// </remarks>
public readonly record struct DwsMeasurement
{
    /// <summary>
    /// 重量（克）
    /// </summary>
    public decimal WeightGrams { get; init; }

    /// <summary>
    /// 长度（毫米）
    /// </summary>
    public decimal LengthMm { get; init; }

    /// <summary>
    /// 宽度（毫米）
    /// </summary>
    public decimal WidthMm { get; init; }

    /// <summary>
    /// 高度（毫米）
    /// </summary>
    public decimal HeightMm { get; init; }

    /// <summary>
    /// 体积重量（克），可选，由上游计算
    /// </summary>
    public decimal? VolumetricWeightGrams { get; init; }

    /// <summary>
    /// 条码（可选）
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 测量时间
    /// </summary>
    public DateTimeOffset MeasuredAt { get; init; }
}

/// <summary>
/// 落格完成通知
/// </summary>
/// <remarks>
/// PR-UPSTREAM02: 新增类型，用于通知上游包裹已完成落格。
/// PR-NOSHADOW-ALL: 使用 ParcelFinalStatus 区分 Success/Timeout/Lost 等结果。
/// </remarks>
public record SortingCompletedNotification
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 实际落格格口ID
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 当 FinalStatus = Lost 时，此字段为 0（包裹已不在输送线上）。
    /// </remarks>
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 落格完成时间
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// 是否成功（false 表示进入异常口或失败）
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// 失败原因（如果失败）
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 包裹最终状态
    /// </summary>
    /// <remarks>
    /// PR-NOSHADOW-ALL: 新增字段，使用现有 ParcelFinalStatus 枚举区分结果类型：
    /// <list type="bullet">
    ///   <item>Success - 成功分拣到目标格口</item>
    ///   <item>Timeout - 超时，包裹仍在输送线上，已导向异常口</item>
    ///   <item>Lost - 丢失，包裹已不在输送线上，无法导向异常口，已从缓存清除</item>
    ///   <item>ExecutionError - 执行错误</item>
    /// </list>
    /// </remarks>
    public Core.Enums.Parcel.ParcelFinalStatus FinalStatus { get; init; } = Core.Enums.Parcel.ParcelFinalStatus.Success;
    
    /// <summary>
    /// 受影响的包裹ID列表（仅在 FinalStatus = Lost 时有值）
    /// </summary>
    /// <remarks>
    /// 当包裹丢失时，在丢失包裹创建之后、丢失检测之前创建的包裹会受到影响，
    /// 这些包裹的任务方向已被改为直行以导向异常格口。
    /// 此字段用于上游系统追踪和处理这些受影响的包裹。
    /// </remarks>
    public IReadOnlyList<long>? AffectedParcelIds { get; init; }
}

/// <summary>
/// 上游消息基接口
/// </summary>
/// <remarks>
/// PR-UPSTREAM-UNIFIED: 统一的消息接口，所有发送到上游的消息都实现此接口
/// </remarks>
public interface IUpstreamMessage
{
    /// <summary>
    /// 消息类型
    /// </summary>
    UpstreamMessageType MessageType { get; }
}

/// <summary>
/// 包裹检测通知消息
/// </summary>
/// <remarks>
/// PR-UPSTREAM-UNIFIED: 封装包裹检测通知，用于SendAsync方法
/// </remarks>
public sealed record ParcelDetectedMessage : IUpstreamMessage
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public UpstreamMessageType MessageType => UpstreamMessageType.ParcelDetected;
}

/// <summary>
/// 落格完成通知消息
/// </summary>
/// <remarks>
/// PR-UPSTREAM-UNIFIED: 封装落格完成通知，用于SendAsync方法
/// </remarks>
public sealed record SortingCompletedMessage : IUpstreamMessage
{
    /// <summary>
    /// 落格完成通知内容
    /// </summary>
    public required SortingCompletedNotification Notification { get; init; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public UpstreamMessageType MessageType => UpstreamMessageType.SortingCompleted;
}

/// <summary>
/// 面板按钮按下通知消息
/// </summary>
/// <remarks>
/// 用于通知上游系统用户按下了哪个面板按钮及系统状态变化
/// </remarks>
public sealed record PanelButtonPressedMessage : IUpstreamMessage
{
    /// <summary>
    /// 按钮类型
    /// </summary>
    public required ZakYip.WheelDiverterSorter.Core.Enums.Hardware.PanelButtonType ButtonType { get; init; }

    /// <summary>
    /// 按钮按下时间
    /// </summary>
    public required DateTimeOffset PressedAt { get; init; }

    /// <summary>
    /// 按钮按下前的系统状态
    /// </summary>
    public required ZakYip.WheelDiverterSorter.Core.Enums.System.SystemState SystemStateBefore { get; init; }

    /// <summary>
    /// 按钮按下后的系统状态
    /// </summary>
    public required ZakYip.WheelDiverterSorter.Core.Enums.System.SystemState SystemStateAfter { get; init; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public UpstreamMessageType MessageType => UpstreamMessageType.PanelButtonPressed;
}
