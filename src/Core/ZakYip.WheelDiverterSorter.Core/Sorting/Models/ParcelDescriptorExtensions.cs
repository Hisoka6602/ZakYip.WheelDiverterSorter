using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Events;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Models;

/// <summary>
/// ParcelDescriptor 的扩展方法
/// </summary>
/// <remarks>
/// 提供从边界层 DTO 到领域模型 ParcelDescriptor 的转换方法。
/// 所有边界层 DTO 在进入业务逻辑时应使用这些扩展方法进行转换。
/// </remarks>
public static class ParcelDescriptorExtensions
{
    /// <summary>
    /// 从 CreateParcelRequest 转换为 ParcelDescriptor
    /// </summary>
    /// <param name="request">创建包裹请求</param>
    /// <returns>包裹描述符</returns>
    public static ParcelDescriptor ToParcelDescriptor(this CreateParcelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ParcelDescriptor
        {
            ParcelId = request.ParcelId,
            Barcode = request.Barcode,
            IngressTime = request.DetectedAt,
            SensorId = request.SensorId,
            Metadata = request.Metadata
        };
    }

    /// <summary>
    /// 从 AssignChuteRequest 转换为 ParcelDescriptor
    /// </summary>
    /// <param name="request">分配格口请求</param>
    /// <returns>包裹描述符</returns>
    public static ParcelDescriptor ToParcelDescriptor(this AssignChuteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ParcelDescriptor
        {
            ParcelId = request.ParcelId,
            Barcode = request.Barcode,
            IngressTime = request.RequestTime,
            SensorId = request.SensorId,
            Metadata = request.Metadata
        };
    }

    /// <summary>
    /// 从 SortingRequest 转换为 ParcelDescriptor
    /// </summary>
    /// <param name="request">分拣请求</param>
    /// <returns>包裹描述符</returns>
    public static ParcelDescriptor ToParcelDescriptor(this SortingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ParcelDescriptor
        {
            ParcelId = request.ParcelId,
            Barcode = request.Barcode,
            IngressTime = request.RequestTime,
            SensorId = request.SensorId,
            Metadata = request.Metadata
        };
    }

    /// <summary>
    /// 从 SortingPipelineContext 转换为 ParcelDescriptor
    /// </summary>
    /// <param name="context">分拣流水线上下文</param>
    /// <returns>包裹描述符</returns>
    public static ParcelDescriptor ToParcelDescriptor(this SortingPipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new ParcelDescriptor
        {
            ParcelId = context.ParcelId,
            Barcode = context.Barcode,
            IngressTime = context.CreatedAt,
            SensorId = null, // SortingPipelineContext 不包含 SensorId
            Metadata = null  // SortingPipelineContext 的扩展数据结构不同
        };
    }

    /// <summary>
    /// 从 ParcelCreatedEventArgs 转换为 ParcelDescriptor
    /// </summary>
    /// <param name="eventArgs">包裹创建事件参数</param>
    /// <returns>包裹描述符</returns>
    public static ParcelDescriptor ToParcelDescriptor(this ParcelCreatedEventArgs eventArgs)
    {
        return new ParcelDescriptor
        {
            ParcelId = eventArgs.ParcelId,
            Barcode = eventArgs.Barcode,
            IngressTime = eventArgs.CreatedAt,
            SensorId = eventArgs.SensorId,
            Metadata = null
        };
    }
}
