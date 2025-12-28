namespace ZakYip.WheelDiverterSorter.Execution.Queues;

/// <summary>
/// 任务原地替换操作的结果
/// </summary>
/// <remarks>
/// <para>用于 ReplaceTasksInPlace 方法的返回值，提供详细的替换操作结果信息。</para>
/// 
/// <para><b>什么是"幽灵任务"（Ghost Tasks）？</b></para>
/// <para>
/// 当上游返回的新路径比初始路径短时（例如从Position 0→5缩短为Position 0→2），
/// 剩余Position（3、4、5）中该包裹的旧任务会成为"幽灵任务"。
/// 因为包裹已在Position 2落格，这些任务永远不会执行，但仍占用队列空间，导致队列污染。
/// </para>
/// 
/// <para><b>幽灵任务示例</b>：</para>
/// <example>
/// 初始路径：Position 0→5（异常口999，全直通）
/// 上游返回：Position 0→2（格口6，Position 2左转）
/// 幽灵任务：Position 3、4、5的旧任务（包裹已不在输送线上，这些任务永不执行）
/// 清理结果：RemovedPositions = [3, 4, 5], RemovedCount = 3
/// </example>
/// </remarks>
public record TaskReplacementResult
{
    /// <summary>
    /// 成功替换的任务数量
    /// </summary>
    public int ReplacedCount { get; init; }
    
    /// <summary>
    /// 未找到任务的Position索引列表（任务可能已被出队执行）
    /// </summary>
    public List<int> NotFoundPositions { get; init; } = new();
    
    /// <summary>
    /// 被跳过的Position索引列表（新任务列表中没有对应的Position）
    /// </summary>
    public List<int> SkippedPositions { get; init; } = new();
    
    /// <summary>
    /// 成功替换的Position索引列表
    /// </summary>
    public List<int> ReplacedPositions { get; init; } = new();
    
    /// <summary>
    /// 被移除的Position索引列表（旧路径有但新路径没有的Position，已清理幽灵任务）
    /// </summary>
    public List<int> RemovedPositions { get; init; } = new();
    
    /// <summary>
    /// 从所有队列中移除的任务总数（幽灵任务清理）
    /// </summary>
    public int RemovedCount { get; init; }
    
    /// <summary>
    /// 替换操作是否完全成功（所有新任务都找到了对应的旧任务并成功替换）
    /// </summary>
    public bool IsFullySuccessful => NotFoundPositions.Count == 0 && SkippedPositions.Count == 0;
    
    /// <summary>
    /// 是否有部分成功（至少替换了一个任务）
    /// </summary>
    public bool IsPartiallySuccessful => ReplacedCount > 0;
}
