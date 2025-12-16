using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 输送线段配置仓储接口
/// </summary>
public interface IConveyorSegmentRepository
{
    /// <summary>
    /// 获取所有线段配置
    /// </summary>
    /// <returns>线段配置列表</returns>
    IEnumerable<ConveyorSegmentConfiguration> GetAll();

    /// <summary>
    /// 根据线段ID获取配置
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>线段配置，如不存在则返回null</returns>
    ConveyorSegmentConfiguration? GetById(long segmentId);

    /// <summary>
    /// 插入新的线段配置
    /// </summary>
    /// <param name="config">线段配置</param>
    void Insert(ConveyorSegmentConfiguration config);

    /// <summary>
    /// 更新线段配置
    /// </summary>
    /// <param name="config">线段配置</param>
    /// <returns>是否更新成功</returns>
    bool Update(ConveyorSegmentConfiguration config);

    /// <summary>
    /// 删除线段配置
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>是否删除成功</returns>
    bool Delete(long segmentId);

    /// <summary>
    /// 检查线段ID是否存在
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>是否存在</returns>
    bool Exists(long segmentId);

    /// <summary>
    /// 批量插入线段配置
    /// </summary>
    /// <param name="configs">线段配置列表</param>
    void InsertBatch(IEnumerable<ConveyorSegmentConfiguration> configs);

    /// <summary>
    /// 批量异步插入线段配置
    /// </summary>
    /// <param name="configs">线段配置列表</param>
    /// <returns>插入的数量</returns>
    Task<int> BulkInsertAsync(IEnumerable<ConveyorSegmentConfiguration> configs);

    /// <summary>
    /// 批量异步更新线段配置
    /// </summary>
    /// <param name="configs">线段配置列表</param>
    /// <returns>更新的数量</returns>
    Task<int> BulkUpdateAsync(IEnumerable<ConveyorSegmentConfiguration> configs);

    /// <summary>
    /// 批量异步获取线段配置
    /// </summary>
    /// <param name="segmentIds">线段ID集合</param>
    /// <returns>线段配置集合</returns>
    Task<IEnumerable<ConveyorSegmentConfiguration>> BulkGetAsync(IEnumerable<long> segmentIds);
}
