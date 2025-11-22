using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Core.LineModel.Utilities;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 系统配置管理API控制器
/// </summary>
/// <remarks>
/// 提供系统配置的查询和更新功能，支持热更新
/// </remarks>
[ApiController]
[Route("api/config/system")]
[Produces("application/json")]
public class SystemConfigController : ControllerBase
{
    private readonly ISystemConfigurationRepository _repository;
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ILogger<SystemConfigController> _logger;

    public SystemConfigController(
        ISystemConfigurationRepository repository,
        IRouteConfigurationRepository routeRepository,
        ILogger<SystemConfigController> logger)
    {
        _repository = repository;
        _routeRepository = routeRepository;
        _logger = logger;
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <returns>系统配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取当前系统配置",
        Description = "返回系统当前的全局配置参数，包括异常处理、通信参数、分拣模式等",
        OperationId = "GetSystemConfig",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(SystemConfigResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SystemConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SystemConfigResponse> GetSystemConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统配置失败");
            return StatusCode(500, new { message = "获取系统配置失败" });
        }
    }

    /// <summary>
    /// 获取默认系统配置模板
    /// </summary>
    /// <returns>默认系统配置模板</returns>
    /// <response code="200">成功返回模板</response>
    /// <remarks>
    /// 返回系统默认配置，可用作配置文件模板
    /// </remarks>
    [HttpGet("template")]
    [SwaggerOperation(
        Summary = "获取默认系统配置模板",
        Description = "返回系统默认配置作为配置模板，可用于初始化或参考",
        OperationId = "GetSystemConfigTemplate",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "成功返回模板", typeof(SystemConfigRequest))]
    [ProducesResponseType(typeof(SystemConfigRequest), 200)]
    public ActionResult<SystemConfigRequest> GetTemplate()
    {
        var defaultConfig = SystemConfiguration.GetDefault();
        return Ok(new SystemConfigRequest
        {
            ExceptionChuteId = defaultConfig.ExceptionChuteId,
            MqttDefaultPort = defaultConfig.MqttDefaultPort,
            TcpDefaultPort = defaultConfig.TcpDefaultPort,
            ChuteAssignmentTimeoutMs = defaultConfig.ChuteAssignmentTimeoutMs,
            RequestTimeoutMs = defaultConfig.RequestTimeoutMs,
            RetryCount = defaultConfig.RetryCount,
            RetryDelayMs = defaultConfig.RetryDelayMs,
            EnableAutoReconnect = defaultConfig.EnableAutoReconnect,
            SortingMode = defaultConfig.SortingMode,
            FixedChuteId = defaultConfig.FixedChuteId,
            AvailableChuteIds = defaultConfig.AvailableChuteIds
        });
    }

    /// <summary>
    /// 更新系统配置（支持热更新）
    /// </summary>
    /// <param name="request">系统配置请求</param>
    /// <returns>更新后的系统配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/system
    ///     {
    ///         "exceptionChuteId": 999,
    ///         "mqttDefaultPort": 1883,
    ///         "tcpDefaultPort": 8888,
    ///         "chuteAssignmentTimeoutMs": 10000,
    ///         "requestTimeoutMs": 5000,
    ///         "retryCount": 3,
    ///         "retryDelayMs": 1000,
    ///         "enableAutoReconnect": true,
    ///         "sortingMode": "Formal"
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务。
    /// 
    /// 分拣模式说明：
    /// - Formal：正式模式，根据上游RuleEngine分配的格口进行分拣
    /// - FixedChute：固定格口模式，所有包裹分拣到指定的固定格口
    /// - RoundRobin：循环格口模式，包裹依次分拣到可用格口列表中的格口
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新系统配置",
        Description = "更新系统全局配置参数，配置会立即生效无需重启。可配置异常处理、通信参数、分拣模式等。",
        OperationId = "UpdateSystemConfig",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(SystemConfigResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SystemConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SystemConfigResponse> UpdateSystemConfig([FromBody] SystemConfigRequest request)
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

            var config = MapToConfiguration(request);
            
            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            // 验证异常格口是否存在于路由配置中
            var exceptionRoute = _routeRepository.GetByChuteId(config.ExceptionChuteId);
            if (exceptionRoute == null)
            {
                _logger.LogWarning("异常格口 {ExceptionChuteId} 不存在于路由配置中", 
                    config.ExceptionChuteId);
                return BadRequest(new { message = $"异常格口 {config.ExceptionChuteId} 不存在于路由配置中，请先创建对应的路由配置" });
            }

            if (!exceptionRoute.IsEnabled)
            {
                _logger.LogWarning("异常格口 {ExceptionChuteId} 的路由配置未启用", 
                    config.ExceptionChuteId);
                return BadRequest(new { message = $"异常格口 {config.ExceptionChuteId} 的路由配置未启用" });
            }

            _repository.Update(config);

            _logger.LogInformation(
                "系统配置已更新: ExceptionChuteId={ExceptionChuteId}, Version={Version}",
                config.ExceptionChuteId,
                config.Version);

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "系统配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系统配置失败");
            return StatusCode(500, new { message = "更新系统配置失败" });
        }
    }

    /// <summary>
    /// 重置系统配置为默认值
    /// </summary>
    /// <returns>重置后的系统配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 将系统配置重置为默认值
    /// </remarks>
    [HttpPost("reset")]
    [SwaggerOperation(
        Summary = "重置系统配置为默认值",
        Description = "将所有系统配置参数重置为默认值，配置立即生效",
        OperationId = "ResetSystemConfig",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "重置成功", typeof(SystemConfigResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SystemConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SystemConfigResponse> ResetSystemConfig()
    {
        try
        {
            var defaultConfig = SystemConfiguration.GetDefault();
            _repository.Update(defaultConfig);

            _logger.LogInformation("系统配置已重置为默认值");

            var updatedConfig = _repository.Get();
            return Ok(MapToResponse(updatedConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置系统配置失败");
            return StatusCode(500, new { message = "重置系统配置失败" });
        }
    }

    /// <summary>
    /// 获取当前分拣模式配置
    /// </summary>
    /// <returns>当前分拣模式配置信息</returns>
    /// <response code="200">成功返回分拣模式配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前系统的分拣模式及相关配置参数。
    /// 
    /// 分拣模式说明：
    /// - Formal：正式分拣模式（默认），由上游 Sorting.RuleEngine 给出格口分配
    /// - FixedChute：指定落格分拣模式，可设置固定格口落格（异常除外），每次都只在指定的格口ID落格
    /// - RoundRobin：循环格口落格模式，包裹依次分拣到可用格口列表中的格口
    /// </remarks>
    [HttpGet("sorting-mode")]
    [SwaggerOperation(
        Summary = "获取当前分拣模式",
        Description = "返回系统当前的分拣模式配置，包括模式类型和相关参数",
        OperationId = "GetSortingMode",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "成功返回分拣模式配置", typeof(SortingModeResponse))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SortingModeResponse), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SortingModeResponse> GetSortingMode()
    {
        try
        {
            var config = _repository.Get();
            return Ok(new SortingModeResponse
            {
                SortingMode = config.SortingMode,
                FixedChuteId = config.FixedChuteId,
                AvailableChuteIds = config.AvailableChuteIds ?? new List<int>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分拣模式配置失败");
            return StatusCode(500, new { message = "获取分拣模式配置失败" });
        }
    }

    /// <summary>
    /// 更新分拣模式配置
    /// </summary>
    /// <param name="request">分拣模式配置请求</param>
    /// <returns>更新后的分拣模式配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 更新系统的分拣模式配置，配置会立即生效无需重启。
    /// 
    /// 分拣模式说明：
    /// - Formal：正式分拣模式（默认），由上游 Sorting.RuleEngine 给出格口分配
    /// - FixedChute：指定落格分拣模式，需要设置 FixedChuteId，所有包裹（异常除外）都将发送到此格口
    /// - RoundRobin：循环格口落格模式，需要设置 AvailableChuteIds，系统会按顺序循环使用这些格口
    /// 
    /// 示例请求（正式分拣模式）：
    /// 
    ///     PUT /api/config/system/sorting-mode
    ///     {
    ///         "sortingMode": "Formal"
    ///     }
    /// 
    /// 示例请求（固定格口模式）：
    /// 
    ///     PUT /api/config/system/sorting-mode
    ///     {
    ///         "sortingMode": "FixedChute",
    ///         "fixedChuteId": 1
    ///     }
    /// 
    /// 示例请求（循环格口模式）：
    /// 
    ///     PUT /api/config/system/sorting-mode
    ///     {
    ///         "sortingMode": "RoundRobin",
    ///         "availableChuteIds": [1, 2, 3, 4, 5, 6]
    ///     }
    /// </remarks>
    [HttpPut("sorting-mode")]
    [SwaggerOperation(
        Summary = "更新分拣模式配置",
        Description = "更新系统的分拣模式配置，配置会立即生效无需重启",
        OperationId = "UpdateSortingMode",
        Tags = new[] { "系统配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(SortingModeResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SortingModeResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SortingModeResponse> UpdateSortingMode([FromBody] SortingModeRequest request)
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

            // 验证分拣模式值
            if (!Enum.IsDefined(typeof(SortingMode), request.SortingMode))
            {
                return BadRequest(new { message = "分拣模式值无效，仅支持：Formal（正常）、FixedChute（指定落格）、RoundRobin（循环落格）" });
            }

            // 获取当前配置
            var config = _repository.Get();

            // 更新分拣模式相关字段
            config.SortingMode = request.SortingMode;
            config.FixedChuteId = request.FixedChuteId;
            config.AvailableChuteIds = request.AvailableChuteIds ?? new List<int>();

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            // 更新配置
            _repository.Update(config);

            _logger.LogInformation(
                "分拣模式已更新: SortingMode={SortingMode}, FixedChuteId={FixedChuteId}, AvailableChuteIds={AvailableChuteIds}",
                config.SortingMode,
                config.FixedChuteId,
                string.Join(",", config.AvailableChuteIds));

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(new SortingModeResponse
            {
                SortingMode = updatedConfig.SortingMode,
                FixedChuteId = updatedConfig.FixedChuteId,
                AvailableChuteIds = updatedConfig.AvailableChuteIds ?? new List<int>()
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "分拣模式配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新分拣模式配置失败");
            return StatusCode(500, new { message = "更新分拣模式配置失败" });
        }
    }

    /// <summary>
    /// 将请求模型映射到配置模型
    /// </summary>
    private SystemConfiguration MapToConfiguration(SystemConfigRequest request)
    {
        return new SystemConfiguration
        {
            ExceptionChuteId = request.ExceptionChuteId,
            MqttDefaultPort = request.MqttDefaultPort,
            TcpDefaultPort = request.TcpDefaultPort,
            ChuteAssignmentTimeoutMs = request.ChuteAssignmentTimeoutMs,
            RequestTimeoutMs = request.RequestTimeoutMs,
            RetryCount = request.RetryCount,
            RetryDelayMs = request.RetryDelayMs,
            EnableAutoReconnect = request.EnableAutoReconnect,
            SortingMode = request.SortingMode,
            FixedChuteId = request.FixedChuteId,
            AvailableChuteIds = request.AvailableChuteIds ?? new List<int>()
        };
    }

    /// <summary>
    /// 将配置模型映射到响应模型
    /// </summary>
    private SystemConfigResponse MapToResponse(SystemConfiguration config)
    {
        return new SystemConfigResponse
        {
            Id = config.Id,
            ExceptionChuteId = config.ExceptionChuteId,
            MqttDefaultPort = config.MqttDefaultPort,
            TcpDefaultPort = config.TcpDefaultPort,
            ChuteAssignmentTimeoutMs = config.ChuteAssignmentTimeoutMs,
            RequestTimeoutMs = config.RequestTimeoutMs,
            RetryCount = config.RetryCount,
            RetryDelayMs = config.RetryDelayMs,
            EnableAutoReconnect = config.EnableAutoReconnect,
            SortingMode = config.SortingMode,
            FixedChuteId = config.FixedChuteId,
            AvailableChuteIds = config.AvailableChuteIds ?? new List<int>(),
            Version = config.Version,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}
