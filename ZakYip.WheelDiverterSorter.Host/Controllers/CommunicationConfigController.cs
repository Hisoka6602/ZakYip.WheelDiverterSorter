using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 通信配置管理API控制器
/// </summary>
/// <remarks>
/// 提供通信配置的查询和更新功能，支持热更新
/// </remarks>
[ApiController]
[Route("api/config/communication")]
[Produces("application/json")]
public class CommunicationConfigController : ControllerBase
{
    private readonly ICommunicationConfigurationRepository _repository;
    private readonly ILogger<CommunicationConfigController> _logger;

    public CommunicationConfigController(
        ICommunicationConfigurationRepository repository,
        ILogger<CommunicationConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取通信配置
    /// </summary>
    /// <returns>通信配置信息</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(CommunicationConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfiguration> GetCommunicationConfig()
    {
        try
        {
            var config = _repository.Get();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取通信配置失败");
            return StatusCode(500, new { message = "获取通信配置失败" });
        }
    }

    /// <summary>
    /// 更新通信配置（支持热更新）
    /// </summary>
    /// <param name="request">通信配置请求</param>
    /// <returns>更新后的通信配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/communication
    ///     {
    ///         "mode": 1,
    ///         "tcpServer": "192.168.1.100:8000",
    ///         "timeoutMs": 5000,
    ///         "retryCount": 3
    ///     }
    /// 
    /// 通信模式: Http=0, Tcp=1, SignalR=2, Mqtt=3
    /// 配置更新后立即生效，无需重启服务
    /// </remarks>
    [HttpPut]
    [ProducesResponseType(typeof(CommunicationConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfiguration> UpdateCommunicationConfig([FromBody] CommunicationConfiguration request)
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

            // 验证配置
            var (isValid, errorMessage) = request.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            _repository.Update(request);

            _logger.LogInformation(
                "通信配置已更新: Mode={Mode}, Version={Version}",
                request.Mode,
                request.Version);

            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "通信配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新通信配置失败");
            return StatusCode(500, new { message = "更新通信配置失败" });
        }
    }

    /// <summary>
    /// 重置通信配置为默认值
    /// </summary>
    /// <returns>重置后的通信配置</returns>
    /// <response code="200">重置成功</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(CommunicationConfiguration), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<CommunicationConfiguration> ResetCommunicationConfig()
    {
        try
        {
            var defaultConfig = CommunicationConfiguration.GetDefault();
            _repository.Update(defaultConfig);

            _logger.LogInformation("通信配置已重置为默认值");

            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置通信配置失败");
            return StatusCode(500, new { message = "重置通信配置失败" });
        }
    }
}
