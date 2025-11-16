using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Simulated;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 面板仿真控制器。
/// 提供 API 端点用于仿真模式下模拟面板按钮操作和查询状态。
/// </summary>
[ApiController]
[Route("api/simulation/panel")]
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
    /// 模拟按下指定按钮。
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <response code="200">操作成功</response>
    /// <response code="400">按钮类型无效或未启用仿真模式</response>
    [HttpPost("press")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    /// 模拟释放指定按钮。
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <response code="200">操作成功</response>
    /// <response code="400">按钮类型无效或未启用仿真模式</response>
    [HttpPost("release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    /// 获取当前面板状态（所有按钮状态）。
    /// </summary>
    /// <response code="200">返回面板状态</response>
    [HttpGet("state")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
