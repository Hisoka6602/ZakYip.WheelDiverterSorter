using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 输送线段配置服务接口
/// </summary>
/// <remarks>
/// 负责输送线段配置的业务逻辑，包括CRUD操作、验证、自动计算等
/// </remarks>
public interface IConveyorSegmentService : ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration.IConveyorSegmentService
{
    /// <summary>
    /// 获取所有线段配置
    /// </summary>
    /// <returns>线段配置列表</returns>
    IEnumerable<ConveyorSegmentConfiguration> GetAllSegments();

    /// <summary>
    /// 创建新的线段配置
    /// </summary>
    /// <param name="config">线段配置</param>
    /// <returns>创建结果</returns>
    Task<ConveyorSegmentOperationResult> CreateSegmentAsync(ConveyorSegmentConfiguration config);

    /// <summary>
    /// 更新线段配置
    /// </summary>
    /// <param name="config">线段配置</param>
    /// <returns>更新结果</returns>
    Task<ConveyorSegmentOperationResult> UpdateSegmentAsync(ConveyorSegmentConfiguration config);

    /// <summary>
    /// 删除线段配置
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>删除结果</returns>
    Task<ConveyorSegmentOperationResult> DeleteSegmentAsync(long segmentId);

    /// <summary>
    /// 批量创建线段配置
    /// </summary>
    /// <param name="configs">线段配置列表</param>
    /// <returns>批量操作结果</returns>
    Task<ConveyorSegmentBatchResult> CreateSegmentsBatchAsync(IEnumerable<ConveyorSegmentConfiguration> configs);

    /// <summary>
    /// 验证线段配置是否合法
    /// </summary>
    /// <param name="config">线段配置</param>
    /// <returns>验证结果：(是否有效, 错误消息)</returns>
    (bool IsValid, string? ErrorMessage) ValidateSegment(ConveyorSegmentConfiguration config);

    /// <summary>
    /// 获取默认线段配置模板
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>默认配置模板</returns>
    ConveyorSegmentConfiguration GetDefaultTemplate(long segmentId);
}

/// <summary>
/// 输送线段操作结果
/// </summary>
public record ConveyorSegmentOperationResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 错误消息（操作失败时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 操作后的线段配置（操作成功时）
    /// </summary>
    public ConveyorSegmentConfiguration? Segment { get; init; }

    public static ConveyorSegmentOperationResult Success(ConveyorSegmentConfiguration segment)
    {
        return new ConveyorSegmentOperationResult
        {
            IsSuccess = true,
            Segment = segment
        };
    }

    public static ConveyorSegmentOperationResult Failure(string errorMessage)
    {
        return new ConveyorSegmentOperationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// 输送线段批量操作结果
/// </summary>
public record ConveyorSegmentBatchResult
{
    /// <summary>
    /// 成功处理的数量
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// 失败处理的数量
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// 错误消息列表
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool IsFullSuccess => FailureCount == 0;
}
