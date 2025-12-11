using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Execution.Infrastructure;

/// <summary>
/// ISystemStateManager 适配器扩展方法
/// 提供与原 ISystemRunStateService 兼容的方法
/// </summary>
public static class SystemStateManagerAdapter
{
    public static async Task<OperationResult> TryHandleStartAsync(this ISystemStateManager manager, CancellationToken ct = default)
    {
        var result = await manager.ChangeStateAsync(SystemState.Running, ct);
        return result.Success 
            ? OperationResult.Success() 
            : OperationResult.Failure(result.ErrorMessage ?? "启动失败");
    }

    public static async Task<OperationResult> TryHandleStopAsync(this ISystemStateManager manager, CancellationToken ct = default)
    {
        var result = await manager.ChangeStateAsync(SystemState.Ready, ct);
        return result.Success 
            ? OperationResult.Success() 
            : OperationResult.Failure(result.ErrorMessage ?? "停止失败");
    }

    public static async Task<OperationResult> TryHandleEmergencyStopAsync(this ISystemStateManager manager, CancellationToken ct = default)
    {
        var result = await manager.ChangeStateAsync(SystemState.EmergencyStop, ct);
        return result.Success 
            ? OperationResult.Success() 
            : OperationResult.Failure(result.ErrorMessage ?? "急停失败");
    }

    public static async Task<OperationResult> TryHandleEmergencyResetAsync(this ISystemStateManager manager, CancellationToken ct = default)
    {
        var result = await manager.ChangeStateAsync(SystemState.Ready, ct);
        return result.Success 
            ? OperationResult.Success() 
            : OperationResult.Failure(result.ErrorMessage ?? "急停复位失败");
    }

    public static OperationResult ValidateParcelCreation(this ISystemStateManager manager)
    {
        var currentState = manager.CurrentState;
        return currentState.AllowsParcelCreation()
            ? OperationResult.Success()
            : OperationResult.Failure(currentState.GetParcelCreationDeniedMessage());
    }
}
