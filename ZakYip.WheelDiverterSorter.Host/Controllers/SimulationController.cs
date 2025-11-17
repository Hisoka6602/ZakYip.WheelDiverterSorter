using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Simulation.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 仿真运行控制 API 控制器
/// </summary>
/// <remarks>
/// 提供仿真场景的启动和管理功能，与系统状态机集成
/// </remarks>
[ApiController]
[Route("api/simulation")]
[Produces("application/json")]
public class SimulationController : ControllerBase
{
    private readonly ISystemStateManager _stateManager;
    private readonly ILogger<SimulationController> _logger;
    private static ISimulationScenarioRunner? _scenarioRunner;
    private static CancellationTokenSource? _simulationCts;
    private static Task? _runningSimulation;
    private static readonly object _lockObject = new();

    public SimulationController(
        ISystemStateManager stateManager,
        ILogger<SimulationController> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    /// <summary>
    /// 注册仿真场景运行器（内部使用）
    /// </summary>
    public static void RegisterScenarioRunner(ISimulationScenarioRunner runner)
    {
        _scenarioRunner = runner;
    }

    /// <summary>
    /// 运行场景 E 长跑仿真
    /// </summary>
    /// <returns>启动结果</returns>
    /// <response code="200">仿真启动成功</response>
    /// <response code="400">系统状态不允许启动</response>
    /// <response code="409">仿真已在运行中</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 场景 E 配置：
    /// - 每 300ms 创建一个包裹
    /// - 总共 1000 个包裹
    /// - 目标格口随机分配
    /// - 异常格口为配置中的 ExceptionChuteId
    /// 
    /// 系统要求：
    /// - 系统状态必须为 Ready
    /// - 启动后系统状态切换为 Running
    /// - 仿真结束后系统状态恢复为 Ready
    /// 
    /// 使用方式：
    /// 1. 通过配置 API 设置仿真参数
    /// 2. 调用此接口启动仿真
    /// 3. 通过 Prometheus/Grafana 观察指标
    /// </remarks>
    [HttpPost("run-scenario-e")]
    [SwaggerOperation(
        Summary = "运行场景 E 长跑仿真",
        Description = "启动场景 E 长跑仿真，按配置参数创建包裹并进行分拣",
        OperationId = "RunScenarioE",
        Tags = new[] { "仿真管理" }
    )]
    [SwaggerResponse(200, "仿真启动成功")]
    [SwaggerResponse(400, "系统状态不允许启动")]
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
                return Conflict(new { message = "仿真已在运行中" });
            }

            // 检查系统状态
            if (_stateManager.CurrentState != SystemState.Ready)
            {
                _logger.LogWarning(
                    "系统状态不允许启动仿真，当前状态: {CurrentState}",
                    _stateManager.CurrentState);
                return BadRequest(new 
                { 
                    message = $"系统状态必须为 Ready 才能启动仿真，当前状态: {_stateManager.CurrentState}" 
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
}

/// <summary>
/// 仿真场景运行器接口
/// </summary>
public interface ISimulationScenarioRunner
{
    /// <summary>
    /// 运行场景 E 长跑仿真
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>仿真任务</returns>
    Task RunScenarioEAsync(CancellationToken cancellationToken = default);
}
