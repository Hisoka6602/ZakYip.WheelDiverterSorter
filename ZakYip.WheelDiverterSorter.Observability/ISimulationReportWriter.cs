namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// 仿真报告写入器接口
/// </summary>
/// <remarks>
/// 负责将仿真结果写入不同格式的报告文件（如 Markdown、HTML 等）
/// </remarks>
public interface ISimulationReportWriter
{
    /// <summary>
    /// 将仿真结果写入 Markdown 格式报告
    /// </summary>
    /// <param name="scenarioName">场景名称</param>
    /// <param name="parcels">包裹时间轴快照集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的报告文件路径</returns>
    Task<string> WriteMarkdownAsync(
        string scenarioName,
        IReadOnlyCollection<ParcelTimelineSnapshot> parcels,
        CancellationToken cancellationToken = default);
}
