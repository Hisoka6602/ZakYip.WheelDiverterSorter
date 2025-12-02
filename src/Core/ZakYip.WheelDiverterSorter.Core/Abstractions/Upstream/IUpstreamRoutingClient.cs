namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

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
/// PR-UPSTREAM02: 移除格口分配请求模式，改为检测通知 + 异步推送 + 落格通知。
/// </remarks>
public interface IUpstreamRoutingClient : IDisposable
{
    /// <summary>
    /// 是否已连接到上游系统
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 格口分配接收事件
    /// </summary>
    /// <remarks>
    /// 当上游系统主动推送格口分配时触发此事件。
    /// 事件参数包含包裹ID、分配的格口ID和DWS数据。
    /// PR-UPSTREAM02: 重命名为 ChuteAssigned（从 ChuteAssignmentReceived）
    /// </remarks>
    event EventHandler<ChuteAssignmentEventArgs>? ChuteAssigned;

    /// <summary>
    /// 连接到上游系统
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否连接成功</returns>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开与上游系统的连接
    /// </summary>
    /// <returns>异步任务</returns>
    Task DisconnectAsync();

    /// <summary>
    /// 通知上游系统包裹已到达（fire-and-forget）
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功发送通知</returns>
    /// <remarks>
    /// 此方法仅发送检测通知，不等待格口分配响应。
    /// 发送即忘记模式：发送失败只记录日志，不重试。
    /// 格口分配将通过 ChuteAssigned 事件异步推送。
    /// </remarks>
    Task<bool> NotifyParcelDetectedAsync(long parcelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知上游系统包裹已完成落格（fire-and-forget）
    /// </summary>
    /// <param name="notification">落格完成通知</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功发送通知</returns>
    /// <remarks>
    /// PR-UPSTREAM02: 新增方法，在包裹实际落格（正常或异常口）时调用。
    /// 发送即忘记模式：发送失败只记录日志，不重试。
    /// </remarks>
    Task<bool> NotifySortingCompletedAsync(SortingCompletedNotification notification, CancellationToken cancellationToken = default);
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
}
