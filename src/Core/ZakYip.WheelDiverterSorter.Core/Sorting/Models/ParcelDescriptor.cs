namespace ZakYip.WheelDiverterSorter.Core.Sorting.Models;

/// <summary>
/// 包裹描述符 - 统一领域读模型
/// </summary>
/// <remarks>
/// <para>
/// 这是整个分拣系统中包裹基础信息的标准领域模型。
/// 所有边界层（Host API、上游通讯层）的 DTO 在进入业务逻辑后应统一转换为此类型。
/// </para>
/// <para><b>设计原则</b>：</para>
/// <list type="bullet">
///   <item>Host API 层的请求 DTO 在 Controller 入口处转换为 ParcelDescriptor</item>
///   <item>上游通讯层的协议 DTO 在适配器中转换为 ParcelDescriptor</item>
///   <item>Execution/Core 层内部统一使用 ParcelDescriptor</item>
/// </list>
/// <para><b>使用示例</b>：</para>
/// <code>
/// // 从 CreateParcelRequest 转换
/// var descriptor = request.ToParcelDescriptor();
/// 
/// // 从 SortingPipelineContext 构造
/// var descriptor = ParcelDescriptor.FromContext(context);
/// </code>
/// </remarks>
public sealed record ParcelDescriptor
{
    /// <summary>
    /// 包裹ID（通常为毫秒时间戳）
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码
    /// </summary>
    /// <remarks>
    /// 可选字段，用于与上游系统关联
    /// </remarks>
    public string? Barcode { get; init; }

    /// <summary>
    /// 入口/检测时间
    /// </summary>
    /// <remarks>
    /// 包裹被检测到（通过传感器触发）的时间点
    /// </remarks>
    public DateTimeOffset IngressTime { get; init; }

    /// <summary>
    /// 检测传感器ID
    /// </summary>
    /// <remarks>
    /// 记录检测到该包裹的传感器标识
    /// </remarks>
    public string? SensorId { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    /// <remarks>
    /// 用于传递额外的业务信息，如重量、尺寸等扩展属性
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
