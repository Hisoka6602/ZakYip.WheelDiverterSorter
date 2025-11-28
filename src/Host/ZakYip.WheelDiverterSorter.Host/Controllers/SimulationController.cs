using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Simulation.Services;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Host.Services.Application;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Host.Models;
using ApplicationServices = ZakYip.WheelDiverterSorter.Application.Services;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 统一仿真管理 API 控制器
/// </summary>
/// <remarks>
/// **⚠️ 重要警告：本控制器所有端点仅供仿真和测试环境使用**
/// 
/// 提供所有仿真相关功能的统一入口，包括：
/// - 场景仿真（长跑测试）
/// - 面板仿真（按钮和信号塔模拟）
/// - 包裹分拣测试（手动触发分拣）
/// - 系统状态控制（启动、停止、急停等）
/// 
/// **生产环境保护**：
/// - 在生产环境中调用这些接口可能会干扰真实分拣数据和系统状态
/// - 部分端点会在生产环境下返回 403 Forbidden 错误
/// - 建议生产环境通过配置禁用此控制器或使用 API Gateway 过滤
/// 
/// 所有仿真相关操作统一在本控制器下，避免端点分散。
/// </remarks>
[ApiController]
[Route("api/simulation")]
[Produces("application/json")]
public class SimulationController : ControllerBase
{
    private readonly ISystemStateManager _stateManager;
    private readonly ISystemClock _clock;
    private readonly IPanelInputReader _panelInputReader;
    private readonly ISignalTowerOutput _signalTowerOutput;
    private readonly ApplicationServices.ISimulationModeProvider _simulationModeProvider;
    private readonly ISimulationScenarioRunner? _scenarioRunner;
    private readonly DebugSortService? _debugSortService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SimulationController> _logger;
    private static CancellationTokenSource? _simulationCts;
    private static Task? _runningSimulation;
    private static readonly object _lockObject = new();

    public SimulationController(
        ISystemStateManager stateManager,
        ISystemClock clock,
        IPanelInputReader panelInputReader,
        ISignalTowerOutput signalTowerOutput,
        ApplicationServices.ISimulationModeProvider simulationModeProvider,
        IWebHostEnvironment environment,
        ILogger<SimulationController> logger,
        ISimulationScenarioRunner? scenarioRunner = null,
        DebugSortService? debugSortService = null)
    {
        _stateManager = stateManager;
        _clock = clock;
        _panelInputReader = panelInputReader;
        _signalTowerOutput = signalTowerOutput;
        _simulationModeProvider = simulationModeProvider;
        _environment = environment;
        _scenarioRunner = scenarioRunner;
        _debugSortService = debugSortService;
        _logger = logger;
    }

    /// <summary>
    /// 运行场景 E 长跑仿真
    /// </summary>
    /// <returns>启动结果</returns>
    /// <response code="200">仿真启动成功</response>
    /// <response code="400">系统状态不允许启动（必须在Ready状态）</response>
    /// <response code="409">仿真已在运行中</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 场景 E 配置：
    /// - 每 300ms 创建一个包裹
    /// - 总共 1000 个包裹
    /// - 目标格口随机分配
    /// - 异常格口为配置中的 ExceptionChuteId
    /// 
    /// <para><b>⚠️ 系统状态要求（严格）：</b></para>
    /// <list type="bullet">
    ///   <item><b>只能在 Ready（等待运行）状态下启动</b></item>
    ///   <item>❌ 不能在 Running（已运行）状态下启动</item>
    ///   <item>❌ 不能在 EmergencyStop（急停）状态下启动</item>
    ///   <item>❌ 不能在 Paused、Faulted、Booting 状态下启动</item>
    ///   <item>✅ 启动后系统状态切换为 Running</item>
    ///   <item>✅ 仿真结束后系统状态恢复为 Ready</item>
    /// </list>
    /// 
    /// 使用方式：
    /// 1. 确保系统状态为 Ready（通过 GET /api/system/state 查询）
    /// 2. 通过配置 API 设置仿真参数
    /// 3. 调用此接口启动仿真
    /// 4. 通过 Prometheus/Grafana 观察指标
    /// </remarks>
    [HttpPost("run-scenario-e")]
    [SwaggerOperation(
        Summary = "运行场景 E 长跑仿真（仅限Ready状态）",
        Description = "启动场景 E 长跑仿真，按配置参数创建包裹并进行分拣。⚠️ 只能在Ready状态下启动，不能在Running或EmergencyStop状态下启动。",
        OperationId = "RunScenarioE",
        Tags = new[] { "仿真管理" }
    )]
    [SwaggerResponse(200, "仿真启动成功")]
    [SwaggerResponse(400, "系统状态不允许启动（当前不在Ready状态）")]
    [SwaggerResponse(409, "仿真已在运行中")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> RunScenarioE()
    {
        lock (_lockObject)
        {
            // 检查是否已有仿真在运行
            if (_runningSimulation != null && !_runningSimulation.IsCompleted)
            {
                _logger.LogWarning("仿真已在运行中，拒绝重复启动");
                return Conflict(new { message = "仿真已在运行中，无法启动新的仿真" });
            }

            // ⚠️ 状态检查：只能在 Ready（等待运行）状态下启动仿真
            // 不能在 Running（已运行）或 EmergencyStop（急停）状态下启动
            if (_stateManager.CurrentState != SystemState.Ready)
            {
                var stateDescription = _stateManager.CurrentState switch
                {
                    SystemState.Running => "系统已在运行中",
                    SystemState.EmergencyStop => "系统处于急停状态",
                    SystemState.Paused => "系统处于暂停状态",
                    SystemState.Faulted => "系统处于故障状态",
                    SystemState.Booting => "系统正在启动中",
                    _ => $"未知状态: {_stateManager.CurrentState}"
                };
                
                _logger.LogWarning(
                    "系统状态不允许启动仿真，当前状态: {CurrentState} ({Description})",
                    _stateManager.CurrentState,
                    stateDescription);
                    
                return BadRequest(new 
                { 
                    message = $"只能在等待运行状态（Ready）下启动仿真。当前状态: {stateDescription}",
                    currentState = _stateManager.CurrentState.ToString(),
                    allowedState = "Ready"
                });
            }

            // 检查场景运行器是否已注册
            if (_scenarioRunner == null)
            {
                _logger.LogError("仿真场景运行器未注册");
                return StatusCode(500, new { message = "仿真场景运行器未初始化" });
            }
        }

        try
        {
            // 切换状态到 Running
            var stateChangeResult = await _stateManager.ChangeStateAsync(SystemState.Running);
            if (!stateChangeResult.Success)
            {
                _logger.LogError(
                    "切换到 Running 状态失败: {ErrorMessage}",
                    stateChangeResult.ErrorMessage);
                return BadRequest(new { message = stateChangeResult.ErrorMessage });
            }

            _logger.LogInformation("系统状态已切换为 Running，准备启动场景 E 仿真");

            // 创建新的取消令牌
            _simulationCts = new CancellationTokenSource();

            // 异步启动仿真
            _runningSimulation = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("开始执行场景 E 长跑仿真...");
                    await _scenarioRunner.RunScenarioEAsync(_simulationCts.Token);
                    _logger.LogInformation("场景 E 仿真执行完成");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("场景 E 仿真被取消");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "场景 E 仿真执行过程中发生错误");
                }
                finally
                {
                    // 恢复到 Ready 状态
                    try
                    {
                        var finalState = await _stateManager.ChangeStateAsync(SystemState.Ready);
                        if (finalState.Success)
                        {
                            _logger.LogInformation("仿真结束，系统状态已恢复为 Ready");
                        }
                        else
                        {
                            _logger.LogWarning(
                                "仿真结束后恢复状态失败: {ErrorMessage}",
                                finalState.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "仿真结束后恢复状态时发生异常");
                    }
                }
            }, _simulationCts.Token);

            return Ok(new 
            { 
                message = "场景 E 仿真已启动",
                systemState = _stateManager.CurrentState.ToString(),
                scenarioDescription = "每 300ms 创建包裹，共 1000 个，目标格口随机分配"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动场景 E 仿真失败");
            
            // 尝试恢复状态
            try
            {
                await _stateManager.ChangeStateAsync(SystemState.Ready);
            }
            catch { }

            return StatusCode(500, new { message = "启动仿真失败" });
        }
    }

    /// <summary>
    /// 停止当前运行的仿真
    /// </summary>
    /// <returns>停止结果</returns>
    /// <response code="200">仿真停止成功</response>
    /// <response code="400">没有运行中的仿真</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("stop")]
    [SwaggerOperation(
        Summary = "停止当前运行的仿真",
        Description = "停止正在运行的仿真任务",
        OperationId = "StopSimulation",
        Tags = new[] { "仿真管理" }
    )]
    [SwaggerResponse(200, "仿真停止成功")]
    [SwaggerResponse(400, "没有运行中的仿真")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> StopSimulation()
    {
        try
        {
            lock (_lockObject)
            {
                if (_runningSimulation == null || _runningSimulation.IsCompleted)
                {
                    return BadRequest(new { message = "没有运行中的仿真" });
                }

                _simulationCts?.Cancel();
            }

            _logger.LogInformation("仿真停止请求已发送");

            // 等待仿真任务完成
            if (_runningSimulation != null)
            {
                await Task.WhenAny(_runningSimulation, Task.Delay(5000));
            }

            return Ok(new { message = "仿真已停止" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止仿真失败");
            return StatusCode(500, new { message = "停止仿真失败" });
        }
    }

    /// <summary>
    /// 获取仿真运行状态
    /// </summary>
    /// <returns>仿真状态信息</returns>
    /// <response code="200">成功返回状态</response>
    [HttpGet("status")]
    [SwaggerOperation(
        Summary = "获取仿真运行状态",
        Description = "查询当前仿真的运行状态",
        OperationId = "GetSimulationStatus",
        Tags = new[] { "仿真管理" }
    )]
    [SwaggerResponse(200, "成功返回状态")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetSimulationStatus()
    {
        lock (_lockObject)
        {
            var isRunning = _runningSimulation != null && !_runningSimulation.IsCompleted;
            
            return Ok(new
            {
                isRunning = isRunning,
                systemState = _stateManager.CurrentState.ToString(),
                scenarioRunner = _scenarioRunner != null ? "已注册" : "未注册"
            });
        }
    }

    #region 面板仿真

    /// <summary>
    /// 模拟按下面板启动按钮（切换系统到运行状态）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">操作失败，例如当前状态不允许启动</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 模拟电柜面板启动按钮，触发系统状态切换到 [运行] 状态。
    /// 适用于测试和自动化场景。
    /// </remarks>
    [HttpPost("panel/start")]
    [SwaggerOperation(
        Summary = "模拟按下启动按钮",
        Description = "仿真模式下模拟按下面板启动按钮，触发系统状态切换",
        OperationId = "SimulatePanelStart",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功", typeof(object))]
    [SwaggerResponse(400, "操作失败")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> SimulatePanelStart()
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
    /// 模拟按下面板停止按钮（切换系统到就绪状态）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">操作失败</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("panel/stop")]
    [SwaggerOperation(
        Summary = "模拟按下停止按钮",
        Description = "仿真模式下模拟按下面板停止按钮，触发系统状态切换到就绪状态",
        OperationId = "SimulatePanelStop",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功")]
    [SwaggerResponse(400, "操作失败")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> SimulatePanelStop()
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
    /// 模拟按下面板急停按钮（切换系统到急停状态）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">操作失败</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("panel/emergency-stop")]
    [SwaggerOperation(
        Summary = "模拟按下急停按钮",
        Description = "仿真模式下模拟按下面板急停按钮，立即触发系统进入急停状态",
        OperationId = "SimulatePanelEmergencyStop",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功")]
    [SwaggerResponse(400, "操作失败")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> SimulatePanelEmergencyStop()
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
    /// <response code="400">操作失败</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("panel/emergency-reset")]
    [SwaggerOperation(
        Summary = "模拟急停复位",
        Description = "仿真模式下模拟急停复位按钮，解除急停状态",
        OperationId = "SimulatePanelEmergencyReset",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功")]
    [SwaggerResponse(400, "操作失败")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> SimulatePanelEmergencyReset()
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
    /// 模拟按下指定按钮（低级API，仅在仿真模式下可用）
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">未启用仿真模式或按钮类型无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("panel/press-button")]
    [SwaggerOperation(
        Summary = "模拟按下按钮（低级API）",
        Description = "仅在仿真模式下可用，直接模拟按下指定按钮的底层操作",
        OperationId = "SimulatePressButton",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功")]
    [SwaggerResponse(400, "未启用仿真模式")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public IActionResult SimulatePressButton([FromQuery] PanelButtonType buttonType)
    {
        try
        {
            if (!_simulationModeProvider.IsSimulationMode())
            {
                _logger.LogWarning("非仿真模式下尝试调用 PressButton 接口");
                return BadRequest(new { error = "仅在仿真模式下可调用该接口" });
            }

            if (_panelInputReader is SimulatedPanelInputReader simulatedReader)
            {
                simulatedReader.SimulatePressButton(buttonType);
                _logger.LogInformation("仿真：按下按钮 {ButtonType}", buttonType);
                return Ok(new { message = $"已模拟按下按钮: {buttonType}", buttonType });
            }

            return BadRequest(new { error = "仿真模式未启用或不支持此操作" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模拟按下按钮失败");
            return BadRequest(new { error = "操作失败，请查看日志获取详细信息" });
        }
    }

    /// <summary>
    /// 模拟释放指定按钮（低级API，仅在仿真模式下可用）
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">未启用仿真模式</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("panel/release-button")]
    [SwaggerOperation(
        Summary = "模拟释放按钮（低级API）",
        Description = "仅在仿真模式下可用，直接模拟释放指定按钮的底层操作",
        OperationId = "SimulateReleaseButton",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功")]
    [SwaggerResponse(400, "未启用仿真模式")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public IActionResult SimulateReleaseButton([FromQuery] PanelButtonType buttonType)
    {
        try
        {
            if (!_simulationModeProvider.IsSimulationMode())
            {
                _logger.LogWarning("非仿真模式下尝试调用 ReleaseButton 接口");
                return BadRequest(new { error = "仅在仿真模式下可调用该接口" });
            }

            if (_panelInputReader is SimulatedPanelInputReader simulatedReader)
            {
                simulatedReader.SimulateReleaseButton(buttonType);
                _logger.LogInformation("仿真：释放按钮 {ButtonType}", buttonType);
                return Ok(new { message = $"已模拟释放按钮: {buttonType}", buttonType });
            }

            return BadRequest(new { error = "仿真模式未启用或不支持此操作" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模拟释放按钮失败");
            return BadRequest(new { error = "操作失败，请查看日志获取详细信息" });
        }
    }

    /// <summary>
    /// 获取面板按钮和信号塔状态
    /// </summary>
    /// <returns>面板状态信息</returns>
    /// <response code="200">成功返回状态</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("panel/state")]
    [SwaggerOperation(
        Summary = "获取面板状态",
        Description = "返回当前面板所有按钮的状态和信号塔的状态信息",
        OperationId = "GetPanelState",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "成功返回状态")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetPanelState()
    {
        try
        {
            var buttonStates = await _panelInputReader.ReadAllButtonStatesAsync();
            var signalStates = await _signalTowerOutput.GetAllChannelStatesAsync();

            return Ok(new
            {
                systemState = _stateManager.CurrentState.ToString(),
                timestamp = _clock.LocalNow,
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取面板状态失败");
            return StatusCode(500, new { error = "获取面板状态失败" });
        }
    }

    /// <summary>
    /// 重置所有按钮状态（仅在仿真模式下可用）
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">未启用仿真模式</response>
    [HttpPost("panel/reset-buttons")]
    [SwaggerOperation(
        Summary = "重置所有按钮状态",
        Description = "仅在仿真模式下可用，重置所有按钮到未按下状态",
        OperationId = "ResetAllButtons",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "操作成功")]
    [SwaggerResponse(400, "未启用仿真模式")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public IActionResult ResetAllButtons()
    {
        try
        {
            if (!_simulationModeProvider.IsSimulationMode())
            {
                _logger.LogWarning("非仿真模式下尝试调用 ResetAllButtons 接口");
                return BadRequest(new { error = "仅在仿真模式下可调用该接口" });
            }

            if (_panelInputReader is SimulatedPanelInputReader simulatedReader)
            {
                simulatedReader.ResetAllButtons();
                _logger.LogInformation("仿真：重置所有按钮状态");
                return Ok(new { message = "已重置所有按钮状态" });
            }

            return BadRequest(new { error = "仿真模式未启用或不支持此操作" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置按钮状态失败");
            return BadRequest(new { error = "操作失败" });
        }
    }

    /// <summary>
    /// 获取信号塔状态变更历史（仅在仿真模式下可用）
    /// </summary>
    /// <returns>状态变更历史</returns>
    /// <response code="200">返回状态变更历史</response>
    /// <response code="400">未启用仿真模式</response>
    [HttpGet("panel/signal-tower-history")]
    [SwaggerOperation(
        Summary = "获取信号塔状态变更历史",
        Description = "仅在仿真模式下可用，返回信号塔状态变化的历史记录",
        OperationId = "GetSignalTowerHistory",
        Tags = new[] { "面板仿真" }
    )]
    [SwaggerResponse(200, "返回状态变更历史")]
    [SwaggerResponse(400, "未启用仿真模式")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public IActionResult GetSignalTowerHistory()
    {
        try
        {
            if (!_simulationModeProvider.IsSimulationMode())
            {
                _logger.LogWarning("非仿真模式下尝试调用 GetSignalTowerHistory 接口");
                return BadRequest(new { error = "仅在仿真模式下可调用该接口" });
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取信号塔历史失败");
            return BadRequest(new { error = "操作失败" });
        }
    }

    #endregion 面板仿真

    #region 分拣测试

    /// <summary>
    /// 手动触发包裹分拣（仅供测试/仿真环境）
    /// </summary>
    /// <param name="request">分拣请求，包含包裹ID和目标格口ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分拣执行结果</returns>
    /// <response code="200">分拣执行成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="403">生产环境禁止调用</response>
    /// <response code="503">服务未配置或不可用</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **⚠️ 重要警告：仅供测试/仿真环境使用，生产环境禁止调用**
    /// 
    /// **功能说明**：
    /// 
    /// 接收包裹ID和目标格口ID，生成摆轮切换路径并执行分拣操作。
    /// 
    /// **环境限制**：
    /// - **仅在测试和仿真环境中可用**
    /// - 在生产环境（Production）中调用将返回 403 错误
    /// - 生产环境下应通过扫码或供包台触发分拣，而非此接口
    /// 
    /// **示例请求**：
    /// ```json
    /// {
    ///   "parcelId": "PKG-20231201-001",
    ///   "targetChuteId": 5
    /// }
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "parcelId": "PKG-20231201-001",
    ///   "targetChuteId": 5,
    ///   "actualChuteId": 5,
    ///   "isSuccess": true,
    ///   "message": "分拣执行成功",
    ///   "pathSegmentCount": 3
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 包裹ID必须唯一，避免与真实包裹冲突
    /// - 目标格口ID必须在系统路由配置中存在
    /// - 此接口会实际触发摆轮动作（在硬件环境下）
    /// </remarks>
    [HttpPost("sort")]
    [SwaggerOperation(
        Summary = "手动触发包裹分拣（仅供测试/仿真环境）",
        Description = "接收包裹ID和目标格口ID，生成并执行摆轮分拣路径。**仅在测试/仿真环境可用，生产环境禁止调用**",
        OperationId = "TriggerTestSort",
        Tags = new[] { "分拣测试" }
    )]
    [SwaggerResponse(200, "分拣执行成功", typeof(DebugSortResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(403, "生产环境禁止调用")]
    [SwaggerResponse(503, "服务未配置或不可用")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(DebugSortResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 503)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> TriggerTestSort(
        [FromBody] DebugSortRequest request,
        CancellationToken cancellationToken)
    {
        // 检查环境：生产环境禁止调用
        if (_environment.IsProduction())
        {
            _logger.LogWarning(
                "生产环境下尝试调用仿真测试接口 /api/simulation/sort，已拒绝。ParcelId: {ParcelId}",
                request.ParcelId);
            
            return StatusCode(403, new
            {
                message = "生产环境下禁止调用仿真测试接口",
                errorCode = "FORBIDDEN_IN_PRODUCTION",
                hint = "此接口仅供开发、测试和仿真环境使用。生产环境下应通过扫码或供包台触发分拣。"
            });
        }

        // 参数验证
        if (string.IsNullOrWhiteSpace(request.ParcelId))
        {
            return BadRequest(new { message = "包裹ID不能为空" });
        }

        if (request.TargetChuteId <= 0)
        {
            return BadRequest(new { message = "目标格口ID必须大于0" });
        }

        // 检查是否注入了 DebugSortService
        if (_debugSortService == null)
        {
            _logger.LogError("DebugSortService 未注册，无法执行调试分拣");
            return StatusCode(503, new 
            { 
                message = "调试分拣服务未配置或不可用",
                hint = "请确保在测试/仿真环境中正确注册 DebugSortService"
            });
        }

        try
        {
            _logger.LogInformation(
                "仿真测试：手动触发分拣，ParcelId: {ParcelId}, TargetChuteId: {TargetChuteId}",
                request.ParcelId,
                request.TargetChuteId);

            var response = await _debugSortService.ExecuteDebugSortAsync(
                request.ParcelId,
                request.TargetChuteId,
                cancellationToken);

            if (response.IsSuccess)
            {
                _logger.LogInformation(
                    "仿真测试：分拣执行成功，ParcelId: {ParcelId}",
                    request.ParcelId);
            }
            else
            {
                _logger.LogWarning(
                    "仿真测试：分拣执行失败，ParcelId: {ParcelId}, Message: {Message}",
                    request.ParcelId,
                    response.Message);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真测试：分拣执行异常，ParcelId: {ParcelId}", request.ParcelId);
            return StatusCode(500, new
            {
                message = "分拣执行失败",
                error = ex.Message
            });
        }
    }

    #endregion 分拣测试
}

/// <summary>
/// 仿真状态模型（为向后兼容保留）
/// </summary>
/// <remarks>
/// 该模型用于 E2E 测试的向后兼容性，实际仿真状态通过 /api/simulation/status 端点返回
/// </remarks>
public class SimulationStatus
{
    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 总包裹数
    /// </summary>
    public int TotalParcels { get; set; }

    /// <summary>
    /// 已完成包裹数
    /// </summary>
    public int CompletedParcels { get; set; }

    /// <summary>
    /// 状态消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedTime { get; set; }
}
