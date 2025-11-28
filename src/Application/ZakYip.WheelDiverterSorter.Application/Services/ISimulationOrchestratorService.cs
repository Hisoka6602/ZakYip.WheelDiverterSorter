using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// 仿真编排服务接口
/// </summary>
/// <remarks>
/// 负责管理仿真场景的运行、停止、状态查询等业务逻辑
/// </remarks>
public interface ISimulationOrchestratorService
{
    /// <summary>
    /// 启动场景 E 长跑仿真
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启动结果</returns>
    Task<SimulationStartResult> StartScenarioEAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止当前运行的仿真
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止结果</returns>
    Task<SimulationStopResult> StopSimulationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取仿真运行状态
    /// </summary>
    /// <returns>仿真状态</returns>
    SimulationStatusResult GetSimulationStatus();

    /// <summary>
    /// 模拟按下面板启动按钮
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<PanelOperationResult> SimulatePanelStartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 模拟按下面板停止按钮
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<PanelOperationResult> SimulatePanelStopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 模拟按下面板急停按钮
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<PanelOperationResult> SimulatePanelEmergencyStopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 模拟信号塔输出
    /// </summary>
    /// <param name="red">红灯状态</param>
    /// <param name="yellow">黄灯状态</param>
    /// <param name="green">绿灯状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<SignalTowerResult> SimulateSignalTowerAsync(bool red, bool yellow, bool green, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建仿真包裹
    /// </summary>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建结果</returns>
    Task<ParcelCreationResult> CreateSimulationParcelAsync(int targetChuteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量创建仿真包裹
    /// </summary>
    /// <param name="count">包裹数量</param>
    /// <param name="intervalMs">创建间隔（毫秒）</param>
    /// <param name="targetChuteIds">目标格口ID列表（可选，为空则随机）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建结果</returns>
    Task<BatchParcelCreationResult> CreateBatchSimulationParcelsAsync(
        int count, 
        int intervalMs, 
        IReadOnlyList<int>? targetChuteIds = null, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 仿真启动结果
/// </summary>
public record SimulationStartResult(bool Success, string Message, string? ErrorCode = null);

/// <summary>
/// 仿真停止结果
/// </summary>
public record SimulationStopResult(bool Success, string Message);

/// <summary>
/// 仿真状态结果
/// </summary>
public record SimulationStatusResult(bool IsRunning, SystemState SystemState, bool ScenarioRunnerRegistered);

/// <summary>
/// 面板操作结果
/// </summary>
public record PanelOperationResult(
    bool Success, 
    string Message, 
    SystemState CurrentState, 
    SystemState? PreviousState = null);

/// <summary>
/// 信号塔操作结果
/// </summary>
public record SignalTowerResult(bool Success, string Message);

/// <summary>
/// 包裹创建结果
/// </summary>
public record ParcelCreationResult(bool Success, string Message, string? ParcelId = null, long? ActualChuteId = null);

/// <summary>
/// 批量包裹创建结果
/// </summary>
public record BatchParcelCreationResult(
    bool Success, 
    string Message, 
    int CreatedCount, 
    List<string> ParcelIds);
