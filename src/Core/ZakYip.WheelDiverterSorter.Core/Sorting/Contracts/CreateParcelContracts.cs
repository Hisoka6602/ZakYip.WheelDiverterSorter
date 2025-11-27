using ZakYip.WheelDiverterSorter.Core.Sorting.Models;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;

/// <summary>
/// 创建包裹请求
/// </summary>
/// <remarks>
/// <para>通知上游规则引擎有新包裹进入系统。</para>
/// <para>
/// 此为边界层契约类型，在进入业务逻辑后应通过
/// <see cref="ParcelDescriptorExtensions.ToParcelDescriptor(CreateParcelRequest)"/>
/// 转换为统一领域模型 <see cref="ParcelDescriptor"/>。
/// </para>
/// </remarks>
public record CreateParcelRequest
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// 检测传感器ID
    /// </summary>
    public string? SensorId { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// 创建包裹响应
/// </summary>
/// <remarks>
/// 上游规则引擎对包裹创建通知的响应
/// </remarks>
public record CreateParcelResponse
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// 响应时间
    /// </summary>
    public DateTimeOffset ResponseTime { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
