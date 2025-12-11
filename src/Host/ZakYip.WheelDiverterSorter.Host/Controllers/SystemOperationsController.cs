using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 系统操作API控制器
/// </summary>
/// <remarks>
/// 提供系统级操作接口，如重启服务等
/// </remarks>
[ApiController]
[Route("api/system")]
[Produces("application/json")]
public class SystemOperationsController : ControllerBase
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ISystemStateManager _stateManager;
    private readonly ILogger<SystemOperationsController> _logger;

    /// <summary>
    /// 停止操作等待时间（毫秒）
    /// </summary>
    private const int StopOperationDelayMs = 2000;

    public SystemOperationsController(
        IHostApplicationLifetime lifetime,
        ISystemStateManager stateManager,
        ILogger<SystemOperationsController> logger)
    {
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 重启服务
    /// </summary>
    /// <remarks>
    /// 触发宿主应用优雅退出并重启。
    /// 此操作会先停止系统运行状态，然后退出进程。
    /// 外部部署工具（如 Windows 服务管理器或 systemd）应配置为自动重启服务。
    ///
    /// 注意：此操作是异步执行的，API 会立即返回 202 状态码，实际退出会在后台进行。
    /// </remarks>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作受理结果</returns>
    /// <response code="202">重启请求已受理，服务正在准备退出</response>
    /// <response code="400">请求已取消</response>
    [HttpPost("restart")]
    [SwaggerOperation(
        Summary = "重启服务",
        Description = "触发宿主应用优雅退出并重启。此操作会先停止系统运行状态，然后退出进程。注意：此操作是异步执行的，API 会立即返回 202 状态码，实际退出会在后台进行。",
        OperationId = "RestartService",
        Tags = new[] { "系统操作" }
    )]
    [SwaggerResponse(202, "重启请求已受理", typeof(ApiResponse<object>))]
    [SwaggerResponse(400, "请求已取消", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<object>), 202)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public ActionResult<ApiResponse<object>> RestartService(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return BadRequest(ApiResponse<object>.BadRequest("请求已取消 - Request cancelled"));
        }

        _logger.LogInformation("收到重启请求，将在后台停止系统并退出应用 - Restart request received, will stop system and exit in background");

        // 后台异步执行，彻底与请求线程解耦，防止异常影响调用方
        _ = Task.Run(async () =>
        {
            try
            {
                // 在退出前确保系统停止
                _logger.LogInformation("【退出流程】步骤1：请求系统停止运行 - [Exit Process] Step 1: Request system stop");
                
                // 请求系统停止（转换到Ready状态）
                try
                {
                    await _stateManager.ChangeStateAsync(Core.Enums.System.SystemState.Ready, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "触发系统停止时发生异常，继续退出流程 - Exception during system stop, continuing exit process");
                }

                _logger.LogInformation("【退出流程】步骤2：等待 {StopOperationDelayMs}ms 以确保停止操作完成", StopOperationDelayMs);
                await Task.Delay(StopOperationDelayMs);

                // 直接使用 Environment.Exit(1) 退出进程，以便外部服务管理器重启
                _logger.LogInformation("【退出流程】步骤3：执行 Environment.Exit(1) 退出进程 - [Exit Process] Step 3: Execute Environment.Exit(1)");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止宿主时发生异常，将强制退出（退出码=1） - Exception during host stop, forcing exit (exit code=1)");
                Environment.Exit(1);
            }
        }, CancellationToken.None);

        // 立即返回 202，提示后台正在处理
        return Accepted(ApiResponse<object>.Ok(new { Accepted = true }, "服务正在准备退出 - Service is preparing to exit"));
    }
}
