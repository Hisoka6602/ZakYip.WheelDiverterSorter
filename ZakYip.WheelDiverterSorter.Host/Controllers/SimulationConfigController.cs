using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 仿真配置管理 API 控制器
/// </summary>
/// <remarks>
/// 提供仿真配置的查询和更新功能，支持运行期调整仿真参数
/// </remarks>
[ApiController]
[Route("api/config/simulation")]
[Produces("application/json")]
public class SimulationConfigController : ControllerBase
{
    private readonly IOptionsMonitor<SimulationOptions> _simulationOptions;
    private readonly ILogger<SimulationConfigController> _logger;
    private static SimulationOptions? _runtimeOptions;

    public SimulationConfigController(
        IOptionsMonitor<SimulationOptions> simulationOptions,
        ILogger<SimulationConfigController> logger)
    {
        _simulationOptions = simulationOptions;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前仿真配置
    /// </summary>
    /// <returns>仿真配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(SimulationConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SimulationConfigResponse> GetSimulationConfig()
    {
        try
        {
            var config = _runtimeOptions ?? _simulationOptions.CurrentValue;
            return Ok(MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取仿真配置失败");
            return StatusCode(500, new { message = "获取仿真配置失败" });
        }
    }

    /// <summary>
    /// 更新仿真配置（支持热更新）
    /// </summary>
    /// <param name="request">仿真配置请求</param>
    /// <returns>更新后的仿真配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/simulation
    ///     {
    ///         "parcelCount": 1000,
    ///         "lineSpeedMmps": 1000,
    ///         "parcelIntervalMs": 300,
    ///         "sortingMode": "RoundRobin",
    ///         "exceptionChuteId": 21,
    ///         "minSafeHeadwayMm": 300,
    ///         "minSafeHeadwayTimeMs": 300
    ///     }
    /// 
    /// 配置更新后立即生效。
    /// </remarks>
    [HttpPut]
    [ProducesResponseType(typeof(SimulationConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SimulationConfigResponse> UpdateSimulationConfig([FromBody] SimulationConfigRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "请求参数无效", errors });
            }

            var config = MapToOptions(request);
            
            // 验证配置
            var validationResult = ValidateConfig(config);
            if (!validationResult.isValid)
            {
                return BadRequest(new { message = validationResult.errorMessage });
            }

            _runtimeOptions = config;

            // Sync to SimulationScenarioRunner for use when running scenarios
            ZakYip.WheelDiverterSorter.Simulation.Services.SimulationScenarioRunner.SetRuntimeOptions(config);

            _logger.LogInformation(
                "仿真配置已更新: ParcelCount={ParcelCount}, Interval={Interval}ms",
                config.ParcelCount,
                config.ParcelInterval.TotalMilliseconds);

            return Ok(MapToResponse(config));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "仿真配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新仿真配置失败");
            return StatusCode(500, new { message = "更新仿真配置失败" });
        }
    }

    private (bool isValid, string? errorMessage) ValidateConfig(SimulationOptions config)
    {
        if (config.ParcelCount <= 0)
        {
            return (false, "包裹数量必须大于 0");
        }

        if (config.LineSpeedMmps <= 0)
        {
            return (false, "线速必须大于 0");
        }

        if (config.ParcelInterval.TotalMilliseconds <= 0)
        {
            return (false, "包裹间隔必须大于 0");
        }

        if (config.ExceptionChuteId <= 0)
        {
            return (false, "异常格口 ID 必须大于 0");
        }

        if (config.MinSafeHeadwayMm.HasValue && config.MinSafeHeadwayMm.Value < 0)
        {
            return (false, "最小空间安全间隔不能为负数");
        }

        if (config.MinSafeHeadwayTime.HasValue && config.MinSafeHeadwayTime.Value.TotalMilliseconds < 0)
        {
            return (false, "最小时间安全间隔不能为负数");
        }

        return (true, null);
    }

    private SimulationOptions MapToOptions(SimulationConfigRequest request)
    {
        return new SimulationOptions
        {
            ParcelCount = request.ParcelCount,
            LineSpeedMmps = request.LineSpeedMmps,
            ParcelInterval = TimeSpan.FromMilliseconds(request.ParcelIntervalMs),
            SortingMode = request.SortingMode,
            FixedChuteIds = request.FixedChuteIds,
            ExceptionChuteId = request.ExceptionChuteId,
            IsEnableRandomFriction = request.IsEnableRandomFriction,
            IsEnableRandomDropout = request.IsEnableRandomDropout,
            FrictionModel = new FrictionModelOptions
            {
                MinFactor = request.FrictionMinFactor,
                MaxFactor = request.FrictionMaxFactor,
                IsDeterministic = request.FrictionIsDeterministic,
                Seed = request.FrictionSeed
            },
            DropoutModel = new DropoutModelOptions
            {
                DropoutProbabilityPerSegment = request.DropoutProbability,
                Seed = request.DropoutSeed
            },
            MinSafeHeadwayMm = request.MinSafeHeadwayMm,
            MinSafeHeadwayTime = request.MinSafeHeadwayTimeMs.HasValue 
                ? TimeSpan.FromMilliseconds(request.MinSafeHeadwayTimeMs.Value) 
                : null,
            DenseParcelStrategy = request.DenseParcelStrategy,
            SensorFault = new SensorFaultOptions
            {
                IsPreDiverterSensorFault = request.IsPreDiverterSensorFault,
                IsEnableSensorJitter = request.IsEnableSensorJitter,
                JitterTriggerCount = request.JitterTriggerCount,
                JitterIntervalMs = request.JitterIntervalMs,
                JitterProbability = request.JitterProbability
            },
            IsEnableVerboseLogging = request.IsEnableVerboseLogging,
            IsPauseAtEnd = false,
            IsSimulationEnabled = request.IsSimulationEnabled
        };
    }

    private SimulationConfigResponse MapToResponse(SimulationOptions config)
    {
        return new SimulationConfigResponse
        {
            ParcelCount = config.ParcelCount,
            LineSpeedMmps = config.LineSpeedMmps,
            ParcelIntervalMs = (int)config.ParcelInterval.TotalMilliseconds,
            SortingMode = config.SortingMode,
            FixedChuteIds = config.FixedChuteIds?.ToList(),
            ExceptionChuteId = config.ExceptionChuteId,
            IsEnableRandomFriction = config.IsEnableRandomFriction,
            IsEnableRandomDropout = config.IsEnableRandomDropout,
            FrictionMinFactor = config.FrictionModel.MinFactor,
            FrictionMaxFactor = config.FrictionModel.MaxFactor,
            FrictionIsDeterministic = config.FrictionModel.IsDeterministic,
            FrictionSeed = config.FrictionModel.Seed,
            DropoutProbability = config.DropoutModel.DropoutProbabilityPerSegment,
            DropoutSeed = config.DropoutModel.Seed,
            MinSafeHeadwayMm = config.MinSafeHeadwayMm,
            MinSafeHeadwayTimeMs = config.MinSafeHeadwayTime.HasValue 
                ? (int)config.MinSafeHeadwayTime.Value.TotalMilliseconds 
                : null,
            DenseParcelStrategy = config.DenseParcelStrategy,
            IsPreDiverterSensorFault = config.SensorFault.IsPreDiverterSensorFault,
            IsEnableSensorJitter = config.SensorFault.IsEnableSensorJitter,
            JitterTriggerCount = config.SensorFault.JitterTriggerCount,
            JitterIntervalMs = config.SensorFault.JitterIntervalMs,
            JitterProbability = config.SensorFault.JitterProbability,
            IsEnableVerboseLogging = config.IsEnableVerboseLogging,
            IsSimulationEnabled = config.IsSimulationEnabled
        };
    }

    /// <summary>
    /// 获取当前运行时配置（如果已通过 API 设置）
    /// </summary>
    public static SimulationOptions? GetRuntimeOptions()
    {
        return _runtimeOptions;
    }
}
