using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 面板仿真控制器
/// </summary>
/// <remarks>
/// 提供API端点用于仿真模式下模拟面板按钮操作和查询状态。
/// 仅在使用模拟驱动器时有效。
/// </remarks>
[ApiController]
[Route("api/simulation/panel")]
[Produces("application/json")]
public class PanelSimulationController : ControllerBase
{
    private readonly IPanelInputReader _panelInputReader;
    private readonly ISignalTowerOutput _signalTowerOutput;
    private readonly ILogger<PanelSimulationController> _logger;

    public PanelSimulationController(
        IPanelInputReader panelInputReader,
        ISignalTowerOutput signalTowerOutput,
        ILogger<PanelSimulationController> logger)
    {
        _panelInputReader = panelInputReader;
        _signalTowerOutput = signalTowerOutput;
        _logger = logger;
    }

    /// <summary>
    /// 模拟按下指定按钮
    /// </summary>
    /// <param name="buttonType">按钮类型，支持的值：Start（启动）、Stop（停止）、Emergency（急停）、Reset（复位）</param>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">按钮类型无效或未启用仿真模式</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 仅在使用模拟驱动器（SimulatedPanelInputReader）时有效。
    /// 使用硬件驱动器时此API将返回400错误。
    /// 
    /// 示例请求：
    /// 
    ///     POST /api/simulation/panel/press?buttonType=Start
    /// </remarks>
    [HttpPost("press")]
    [SwaggerOperation(
        Summary = "模拟按下面板按钮",
        Description = "仿真模式下模拟按下指定的面板按钮，用于测试按钮响应逻辑",
        OperationId = "PressButton",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功", typeof(object))]
    [SwaggerResponse(400, "按钮类型无效或未启用仿真模式")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult PressButton([FromQuery] PanelButtonType buttonType)
    {
        if (_panelInputReader is SimulatedPanelInputReader simulatedReader)
        {
            simulatedReader.SimulatePressButton(buttonType);
            _logger.LogInformation("仿真：按下按钮 {ButtonType}", buttonType);
            return Ok(new { message = $"已模拟按下按钮: {buttonType}", buttonType });
        }

        return BadRequest(new { error = "仿真模式未启用或不支持此操作" });
    }

    /// <summary>
    /// 模拟释放指定按钮
    /// </summary>
    /// <param name="buttonType">按钮类型，支持的值：Start（启动）、Stop（停止）、Emergency（急停）、Reset（复位）</param>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">按钮类型无效或未启用仿真模式</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 仿真模式下模拟释放按钮，通常与press配合使用模拟完整的按钮按下-释放过程
    /// </remarks>
    [HttpPost("release")]
    [SwaggerOperation(
        Summary = "模拟释放面板按钮",
        Description = "仿真模式下模拟释放指定的面板按钮，用于完成按钮按下-释放的完整测试流程",
        OperationId = "ReleaseButton",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功", typeof(object))]
    [SwaggerResponse(400, "按钮类型无效或未启用仿真模式")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult ReleaseButton([FromQuery] PanelButtonType buttonType)
    {
        if (_panelInputReader is SimulatedPanelInputReader simulatedReader)
        {
            simulatedReader.SimulateReleaseButton(buttonType);
            _logger.LogInformation("仿真：释放按钮 {ButtonType}", buttonType);
            return Ok(new { message = $"已模拟释放按钮: {buttonType}", buttonType });
        }

        return BadRequest(new { error = "仿真模式未启用或不支持此操作" });
    }

    /// <summary>
    /// 获取当前面板状态
    /// </summary>
    /// <returns>面板所有按钮和信号塔的状态信息</returns>
    /// <response code="200">成功返回面板状态</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回包括：
    /// - buttons: 所有按钮的当前状态（是否按下、最后变化时间、按下持续时间）
    /// - signalTower: 信号塔各通道的状态（是否激活、是否闪烁、闪烁间隔）
    /// </remarks>
    [HttpGet("state")]
    [SwaggerOperation(
        Summary = "获取面板状态",
        Description = "返回当前面板所有按钮的状态和信号塔的状态信息",
        OperationId = "GetPanelState",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "成功返回面板状态", typeof(object))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPanelState()
    {
        var buttonStates = await _panelInputReader.ReadAllButtonStatesAsync();
        var signalStates = await _signalTowerOutput.GetAllChannelStatesAsync();

        return Ok(new
        {
            buttons = buttonStates.Select(kvp => new
            {
                buttonType = kvp.Key.ToString(),
                isPressed = kvp.Value.IsPressed,
                lastChangedAt = kvp.Value.LastChangedAt,
                pressedDurationMs = kvp.Value.PressedDurationMs
            }),
            signalTower = signalStates.Select(kvp => new
            {
                channel = kvp.Key.ToString(),
                isActive = kvp.Value.IsActive,
                isBlinking = kvp.Value.IsBlinking,
                blinkIntervalMs = kvp.Value.BlinkIntervalMs
            })
        });
    }

    /// <summary>
    /// 重置所有按钮状态。
    /// </summary>
    /// <response code="200">操作成功</response>
    /// <response code="400">未启用仿真模式</response>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ResetAllButtons()
    {
        if (_panelInputReader is SimulatedPanelInputReader simulatedReader)
        {
            simulatedReader.ResetAllButtons();
            _logger.LogInformation("仿真：重置所有按钮状态");
            return Ok(new { message = "已重置所有按钮状态" });
        }

        return BadRequest(new { error = "仿真模式未启用或不支持此操作" });
    }

    /// <summary>
    /// 获取信号塔状态变更历史（仅在仿真模式下可用）。
    /// </summary>
    /// <response code="200">返回状态变更历史</response>
    /// <response code="400">未启用仿真模式</response>
    [HttpGet("signal-tower/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetSignalTowerHistory()
    {
        if (_signalTowerOutput is SimulatedSignalTowerOutput simulatedOutput)
        {
            var history = simulatedOutput.GetStateChangeHistory();
            return Ok(new
            {
                count = history.Count,
                changes = history.Select(change => new
                {
                    channel = change.State.Channel.ToString(),
                    isActive = change.State.IsActive,
                    isBlinking = change.State.IsBlinking,
                    changedAt = change.ChangedAt
                })
            });
        }

        return BadRequest(new { error = "仿真模式未启用或不支持此操作" });
    }
}
