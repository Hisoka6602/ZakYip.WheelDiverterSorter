using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// IO 联动执行器接口。
/// 负责执行 IO 联动点的写入操作。
/// </summary>
public interface IIoLinkageExecutor
{
    /// <summary>
    /// 执行指定的 IO 联动点写入
    /// </summary>
    /// <param name="linkagePoints">要写入的 IO 联动点列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<OperationResult> ExecuteAsync(
        IReadOnlyList<IoLinkagePoint> linkagePoints,
        CancellationToken cancellationToken = default);
}
