using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

/// <summary>
/// 输送线段配置查询接口（只读）
/// </summary>
/// <remarks>
/// 提供输送线段配置的查询功能，使用缓存提高性能。
/// 此接口仅用于热路径查询，不包含写操作。
/// </remarks>
public interface IConveyorSegmentQuery
{
    /// <summary>
    /// 根据线段ID获取配置（使用缓存）
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>线段配置，如不存在则返回null</returns>
    ConveyorSegmentConfiguration? GetSegmentById(long segmentId);
}
