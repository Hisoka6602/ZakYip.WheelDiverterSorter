namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 表示从入口到目标格口的完整摆轮路径
/// </summary>
public record class SwitchingPath
{
    /// <summary>
    /// 目标格口标识（数字ID）
    /// </summary>
    public required int TargetChuteId { get; init; }

    /// <summary>
    /// 路径中的所有摆轮段，按顺序排列
    /// </summary>
    public required IReadOnlyList<SwitchingPathSegment> Segments { get; init; }

    /// <summary>
    /// 路径生成时间
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// 最终异常格口（Fallback/Exception Chute）标识（数字ID）
    /// </summary>
    /// <remarks>
    /// <para><strong>用途：</strong>防止包裹滞留造成堵塞，与"单段TTL"配合使用。</para>
    /// <para>当路径执行过程中任意一段失败（如超时、设备故障等）时，
    /// 包裹将被引导到此异常格口，确保包裹能够安全离开分拣系统，避免堵塞输送线。</para>
    /// <para><strong>注意：</strong>如果项目中存在旧的"异常口"处理逻辑（如硬编码的异常口ID），
    /// 可以统一迁移到本字段，删除旧的实现方式。</para>
    /// </remarks>
    public required int FallbackChuteId { get; init; }
}
