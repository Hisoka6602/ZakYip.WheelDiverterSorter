using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Host.StateMachine;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 仿真面板控制 API 控制器
/// </summary>
/// <remarks>
/// 提供仿真模式下的面板按钮模拟操作和系统运行状态控制功能。
/// 这些端点仅用于测试/仿真，不直接操作 IO，而是调用系统状态管理器。
/// </remarks>
[ApiController]
[Route("api/sim/panel")]
[Produces("application/json")]
public class SimulationPanelController : ControllerBase
{
    private readonly ISystemStateManager _stateManager;
    private readonly ISystemClock _clock;
    private readonly ILogger<SimulationPanelController> _logger;

    public SimulationPanelController(
        ISystemStateManager stateManager,
        ISystemClock clock,
        ILogger<SimulationPanelController> logger)
    {
        _stateManager = stateManager;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 模拟按下启动按钮（切换系统到运行状态）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">操作失败，例如当前状态不允许启动</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 模拟电柜面板启动按钮，触发系统状态切换到 [运行] 状态。
    /// </remarks>
    [HttpPost("start")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> Start()
    {
        try
        {
            var result = await _stateManager.ChangeStateAsync(SystemState.Running);
            
            if (result.Success)
            {
                _logger.LogInformation("仿真：启动按钮已按下，系统切换到运行状态");
                return Ok(new 
                { 
                    success = true,
                    message = "系统已启动",
                    currentState = result.CurrentState.ToString(),
                    previousState = result.PreviousState?.ToString()
                });
            }

            _logger.LogWarning("仿真：启动操作失败 - {ErrorMessage}", result.ErrorMessage);
            return BadRequest(new 
            { 
                success = false,
                message = result.ErrorMessage,
                currentState = result.CurrentState.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真：启动操作异常");
            return StatusCode(500, new { success = false, message = "启动操作失败" });
        }
    }

    /// <summary>
    /// 模拟按下停止按钮（切换系统到就绪状态）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">操作失败，例如当前状态不允许停止</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 模拟电柜面板停止按钮，触发系统状态切换到 [就绪] 状态。
    /// </remarks>
    [HttpPost("stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> Stop()
    {
        try
        {
            var result = await _stateManager.ChangeStateAsync(SystemState.Ready);
            
            if (result.Success)
            {
                _logger.LogInformation("仿真：停止按钮已按下，系统切换到就绪状态");
                return Ok(new 
                { 
                    success = true,
                    message = "系统已停止",
                    currentState = result.CurrentState.ToString(),
                    previousState = result.PreviousState?.ToString()
                });
            }

            _logger.LogWarning("仿真：停止操作失败 - {ErrorMessage}", result.ErrorMessage);
            return BadRequest(new 
            { 
                success = false,
                message = result.ErrorMessage,
                currentState = result.CurrentState.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真：停止操作异常");
            return StatusCode(500, new { success = false, message = "停止操作失败" });
        }
    }

    /// <summary>
    /// 模拟按下急停按钮（切换系统到急停状态）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">操作失败</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 模拟电柜面板急停按钮，立即触发系统进入 [急停] 状态。
    /// 所有运动停止，需要通过急停复位才能恢复。
    /// </remarks>
    [HttpPost("emergency-stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> EmergencyStop()
    {
        try
        {
            var result = await _stateManager.ChangeStateAsync(SystemState.EmergencyStop);
            
            if (result.Success)
            {
                _logger.LogWarning("仿真：急停按钮已按下，系统进入急停状态");
                return Ok(new 
                { 
                    success = true,
                    message = "系统已急停",
                    currentState = result.CurrentState.ToString(),
                    previousState = result.PreviousState?.ToString()
                });
            }

            _logger.LogWarning("仿真：急停操作失败 - {ErrorMessage}", result.ErrorMessage);
            return BadRequest(new 
            { 
                success = false,
                message = result.ErrorMessage,
                currentState = result.CurrentState.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真：急停操作异常");
            return StatusCode(500, new { success = false, message = "急停操作失败" });
        }
    }

    /// <summary>
    /// 模拟急停复位（解除急停状态）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">操作失败，例如当前不在急停状态</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 模拟电柜面板急停复位按钮，解除急停状态，系统切换回 [就绪] 状态。
    /// </remarks>
    [HttpPost("emergency-reset")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> EmergencyReset()
    {
        try
        {
            var result = await _stateManager.ChangeStateAsync(SystemState.Ready);
            
            if (result.Success)
            {
                _logger.LogInformation("仿真：急停复位按钮已按下，系统解除急停");
                return Ok(new 
                { 
                    success = true,
                    message = "急停已解除",
                    currentState = result.CurrentState.ToString(),
                    previousState = result.PreviousState?.ToString()
                });
            }

            _logger.LogWarning("仿真：急停复位操作失败 - {ErrorMessage}", result.ErrorMessage);
            return BadRequest(new 
            { 
                success = false,
                message = result.ErrorMessage,
                currentState = result.CurrentState.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真：急停复位操作异常");
            return StatusCode(500, new { success = false, message = "急停复位操作失败" });
        }
    }

    /// <summary>
    /// 获取当前系统运行状态
    /// </summary>
    /// <returns>系统运行状态信息</returns>
    /// <response code="200">成功返回状态</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 查询当前系统运行状态。
    /// </remarks>
    [HttpGet("state")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public IActionResult GetState()
    {
        try
        {
            var currentState = _stateManager.CurrentState;
            var canCreateParcel = currentState == SystemState.Running;

            return Ok(new 
            { 
                currentState = currentState.ToString(),
                stateValue = (int)currentState,
                canCreateParcel = canCreateParcel,
                timestamp = _clock.LocalNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真：获取状态异常");
            return StatusCode(500, new { message = "获取状态失败" });
        }
    }
}
