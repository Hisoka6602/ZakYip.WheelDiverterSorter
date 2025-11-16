using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// IO 联动配置管理 API 控制器
/// </summary>
/// <remarks>
/// 提供 IO 联动配置的查询、更新和手动触发功能
/// </remarks>
[ApiController]
[Route("api/config/io-linkage")]
[Produces("application/json")]
public class IoLinkageController : ControllerBase
{
    private readonly ISystemConfigurationRepository _repository;
    private readonly IIoLinkageDriver _ioLinkageDriver;
    private readonly IIoLinkageCoordinator _ioLinkageCoordinator;
    private readonly ILogger<IoLinkageController> _logger;

    public IoLinkageController(
        ISystemConfigurationRepository repository,
        IIoLinkageDriver ioLinkageDriver,
        IIoLinkageCoordinator ioLinkageCoordinator,
        ILogger<IoLinkageController> logger)
    {
        _repository = repository;
        _ioLinkageDriver = ioLinkageDriver;
        _ioLinkageCoordinator = ioLinkageCoordinator;
        _logger = logger;
    }

    /// <summary>
    /// 获取 IO 联动配置
    /// </summary>
    /// <returns>IO 联动配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(IoLinkageConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IoLinkageConfigResponse> GetIoLinkageConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(MapToResponse(config.IoLinkage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 IO 联动配置失败");
            return StatusCode(500, new { message = "获取 IO 联动配置失败" });
        }
    }

    /// <summary>
    /// 更新 IO 联动配置（支持热更新）
    /// </summary>
    /// <param name="request">IO 联动配置请求</param>
    /// <returns>更新后的 IO 联动配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/io-linkage
    ///     {
    ///         "enabled": true,
    ///         "runningStateIos": [
    ///             { "bitNumber": 3, "level": "ActiveLow" },
    ///             { "bitNumber": 5, "level": "ActiveLow" }
    ///         ],
    ///         "stoppedStateIos": [
    ///             { "bitNumber": 3, "level": "ActiveHigh" },
    ///             { "bitNumber": 5, "level": "ActiveHigh" }
    ///         ]
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务
    /// </remarks>
    [HttpPut]
    [ProducesResponseType(typeof(IoLinkageConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IoLinkageConfigResponse> UpdateIoLinkageConfig([FromBody] IoLinkageConfigRequest request)
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

            // 获取现有系统配置
            var systemConfig = _repository.Get();
            
            // 更新 IoLinkage 配置
            systemConfig.IoLinkage = MapToOptions(request);
            
            // 保存配置
            _repository.Update(systemConfig);

            _logger.LogInformation(
                "IO 联动配置已更新: Enabled={Enabled}, RunningIos={RunningCount}, StoppedIos={StoppedCount}",
                systemConfig.IoLinkage.Enabled,
                systemConfig.IoLinkage.RunningStateIos.Count,
                systemConfig.IoLinkage.StoppedStateIos.Count);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig.IoLinkage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 IO 联动配置失败");
            return StatusCode(500, new { message = "更新 IO 联动配置失败" });
        }
    }

    /// <summary>
    /// 手动触发 IO 联动
    /// </summary>
    /// <param name="systemState">目标系统状态</param>
    /// <returns>触发结果</returns>
    /// <response code="200">触发成功</response>
    /// <response code="400">参数无效或 IO 联动未启用</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     POST /api/io-linkage/trigger?systemState=Running
    /// 
    /// 允许的系统状态: Running, Stopped, Standby
    /// </remarks>
    [HttpPost("trigger")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> TriggerIoLinkage([FromQuery] SystemOperatingState systemState)
    {
        try
        {
            var config = _repository.Get();

            if (!config.IoLinkage.Enabled)
            {
                return BadRequest(new { message = "IO 联动功能未启用" });
            }

            // 确定需要设置的 IO 点
            var ioPoints = _ioLinkageCoordinator.DetermineIoLinkagePoints(systemState, config.IoLinkage);

            if (ioPoints.Count == 0)
            {
                return BadRequest(new 
                { 
                    message = $"系统状态 {systemState} 下无 IO 联动配置" 
                });
            }

            // 执行 IO 联动
            await _ioLinkageDriver.SetIoPointsAsync(ioPoints);

            _logger.LogInformation(
                "手动触发 IO 联动成功: SystemState={SystemState}, IoPointsCount={Count}",
                systemState,
                ioPoints.Count);

            return Ok(new
            {
                message = "IO 联动触发成功",
                systemState = systemState.ToString(),
                triggeredIoPoints = ioPoints.Select(p => new
                {
                    bitNumber = p.BitNumber,
                    level = p.Level.ToString()
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发 IO 联动失败: SystemState={SystemState}", systemState);
            return StatusCode(500, new { message = "触发 IO 联动失败" });
        }
    }

    /// <summary>
    /// 查询指定 IO 点的状态
    /// </summary>
    /// <param name="bitNumber">IO 端口编号</param>
    /// <returns>IO 点状态</returns>
    /// <response code="200">查询成功</response>
    /// <response code="400">参数无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("status/{bitNumber}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GetIoPointStatus(int bitNumber)
    {
        try
        {
            if (bitNumber < 0 || bitNumber > 1023)
            {
                return BadRequest(new { message = "IO 端口编号必须在 0-1023 之间" });
            }

            var state = await _ioLinkageDriver.ReadIoPointAsync(bitNumber);

            return Ok(new
            {
                bitNumber,
                state,
                level = state ? "High" : "Low"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 IO 点状态失败: BitNumber={BitNumber}", bitNumber);
            return StatusCode(500, new { message = "查询 IO 点状态失败" });
        }
    }

    /// <summary>
    /// 将请求模型映射到配置选项
    /// </summary>
    private IoLinkageOptions MapToOptions(IoLinkageConfigRequest request)
    {
        return new IoLinkageOptions
        {
            Enabled = request.Enabled,
            RunningStateIos = request.RunningStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            StoppedStateIos = request.StoppedStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList()
        };
    }

    /// <summary>
    /// 将配置选项映射到响应模型
    /// </summary>
    private IoLinkageConfigResponse MapToResponse(IoLinkageOptions options)
    {
        return new IoLinkageConfigResponse
        {
            Enabled = options.Enabled,
            RunningStateIos = options.RunningStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList(),
            StoppedStateIos = options.StoppedStateIos
                .Select(p => new IoLinkagePointResponse
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                })
                .ToList()
        };
    }
}
