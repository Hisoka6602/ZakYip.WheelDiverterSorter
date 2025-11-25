using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 数递鸟摆轮配置管理API控制器
/// </summary>
/// <remarks>
/// 提供数递鸟摆轮设备的专用配置管理功能，包括：
/// - 查询数递鸟摆轮设备配置
/// - 添加/更新/删除数递鸟设备
/// - 切换仿真模式
/// 
/// **数递鸟摆轮设备说明**：
/// - 通过TCP协议通信
/// - 每个设备有独立的IP、端口和设备地址
/// - 支持多设备配置（不同设备地址）
/// - 支持仿真模式用于测试
/// 
/// **配置生效时机**：
/// 配置更新后立即生效，无需重启服务。系统会自动断开旧连接并建立新连接。
/// 正在运行的分拣任务不受影响，只对新的分拣任务生效。
/// 
/// **注意**：此API与 /api/config/wheel-bindings（摆轮硬件绑定）配合使用。
/// - 本API配置数递鸟设备的TCP连接参数
/// - 摆轮硬件绑定API配置逻辑摆轮节点与驱动器的映射关系
/// </remarks>
[ApiController]
[Route("api/config/wheeldiverter/shudiniao")]
[Produces("application/json")]
public class ShuDiNiaoConfigController : ControllerBase
{
    private readonly IWheelDiverterConfigurationRepository _repository;
    private readonly IWheelDiverterDriverManager? _driverManager;
    private readonly ILogger<ShuDiNiaoConfigController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="repository">摆轮配置仓储</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="driverManager">
    /// 驱动管理器（可选）。
    /// 在以下情况下可能为null：
    /// - 仿真模式运行时，不需要真实驱动管理器
    /// - 服务启动阶段，驱动管理器尚未注册
    /// 当驱动管理器为null时，配置更新只会持久化，不会执行热更新连接操作。
    /// </param>
    public ShuDiNiaoConfigController(
        IWheelDiverterConfigurationRepository repository,
        ILogger<ShuDiNiaoConfigController> logger,
        IWheelDiverterDriverManager? driverManager = null)
    {
        _repository = repository;
        _logger = logger;
        _driverManager = driverManager;
    }

    /// <summary>
    /// 获取数递鸟摆轮配置
    /// </summary>
    /// <returns>数递鸟摆轮配置信息，如果未配置则返回null</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取数递鸟摆轮配置",
        Description = "返回当前系统的数递鸟摆轮设备配置，包括所有设备列表和仿真模式设置",
        OperationId = "GetShuDiNiaoConfig",
        Tags = new[] { "数递鸟摆轮配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ShuDiNiaoWheelDiverterConfig))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ShuDiNiaoWheelDiverterConfig), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ShuDiNiaoWheelDiverterConfig?> GetShuDiNiaoConfig()
    {
        try
        {
            var config = _repository.Get();
            if (config.ShuDiNiao == null)
            {
                // 返回默认空配置对象，而不是null（避免204 No Content）
                return Ok(new ShuDiNiaoWheelDiverterConfig
                {
                    Devices = new List<ShuDiNiaoDeviceEntry>(),
                    UseSimulation = false
                });
            }
            return Ok(config.ShuDiNiao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取数递鸟摆轮配置失败");
            return StatusCode(500, new { message = "获取数递鸟摆轮配置失败" });
        }
    }

    /// <summary>
    /// 更新数递鸟摆轮配置
    /// </summary>
    /// <param name="request">数递鸟摆轮配置请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的完整摆轮配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     PUT /api/config/wheeldiverter/shudiniao
    ///     {
    ///         "devices": [
    ///             {
    ///                 "diverterId": "D1",
    ///                 "host": "192.168.0.100",
    ///                 "port": 2000,
    ///                 "deviceAddress": 81,
    ///                 "isEnabled": true
    ///             },
    ///             {
    ///                 "diverterId": "D2",
    ///                 "host": "192.168.0.100",
    ///                 "port": 2000,
    ///                 "deviceAddress": 82,
    ///                 "isEnabled": true
    ///             }
    ///         ],
    ///         "useSimulation": false
    ///     }
    /// 
    /// 配置更新后立即生效，无需重启服务。系统会自动断开旧连接并建立新连接。
    /// 注意：正在运行的分拣任务不受影响，只对新的分拣任务生效。
    /// 
    /// **设备地址说明**：
    /// - 0x51 (十进制81) 表示 1 号设备
    /// - 0x52 (十进制82) 表示 2 号设备
    /// - 0x53 (十进制83) 表示 3 号设备
    /// - 依次类推
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新数递鸟摆轮配置",
        Description = "更新数递鸟摆轮设备配置，支持多设备和仿真模式切换。配置更新后会自动执行热更新（断开旧连接并建立新连接）。",
        OperationId = "UpdateShuDiNiaoConfig",
        Tags = new[] { "数递鸟摆轮配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(WheelDiverterConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(WheelDiverterConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<WheelDiverterConfiguration>> UpdateShuDiNiaoConfig(
        [FromBody] UpdateShuDiNiaoConfigRequest request,
        CancellationToken cancellationToken = default)
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

            // 获取当前配置
            var config = _repository.Get();

            // 更新数递鸟配置
            config.ShuDiNiao = new ShuDiNiaoWheelDiverterConfig
            {
                Devices = request.Devices.Select(d => new ShuDiNiaoDeviceEntry
                {
                    DiverterId = d.DiverterId,
                    Host = d.Host,
                    Port = d.Port,
                    DeviceAddress = d.DeviceAddress,
                    LeftChuteId = d.LeftChuteId,
                    RightChuteId = d.RightChuteId,
                    IsEnabled = d.IsEnabled
                }).ToList(),
                UseSimulation = request.UseSimulation
            };

            // 如果启用了数递鸟配置，则将厂商类型设置为ShuDiNiao
            if (request.Devices.Any())
            {
                config.VendorType = Core.Enums.Hardware.WheelDiverterVendorType.ShuDiNiao;
            }

            // 验证配置
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                return BadRequest(new { message = errorMessage });
            }

            // 保存配置
            _repository.Update(config);

            _logger.LogInformation(
                "数递鸟摆轮配置已更新: 设备数量={DeviceCount}, 仿真模式={UseSimulation}",
                request.Devices.Count,
                request.UseSimulation);

            // 应用热更新：断开旧连接并建立新连接
            if (_driverManager != null)
            {
                var applyResult = await _driverManager.ApplyConfigurationAsync(config, cancellationToken);
                
                if (applyResult.IsSuccess)
                {
                    _logger.LogInformation(
                        "数递鸟摆轮驱动器热更新成功: 已连接={ConnectedCount}/{TotalCount}",
                        applyResult.ConnectedCount,
                        applyResult.TotalCount);
                }
                else
                {
                    _logger.LogWarning(
                        "数递鸟摆轮驱动器热更新部分失败: {ErrorMessage}, 失败设备={FailedDrivers}",
                        applyResult.ErrorMessage,
                        string.Join(", ", applyResult.FailedDriverIds));
                }
            }
            else
            {
                _logger.LogDebug("驱动管理器未注册，跳过热更新（可能是仿真模式或启动阶段）");
            }

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "数递鸟摆轮配置验证失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新数递鸟摆轮配置失败");
            return StatusCode(500, new { message = "更新数递鸟摆轮配置失败" });
        }
    }

    /// <summary>
    /// 切换数递鸟摆轮仿真模式
    /// </summary>
    /// <param name="useSimulation">是否使用仿真模式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的完整摆轮配置</returns>
    /// <response code="200">切换成功</response>
    /// <response code="400">请求参数无效或未配置数递鸟设备</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     POST /api/config/wheeldiverter/shudiniao/simulation?useSimulation=true
    /// 
    /// 仿真模式说明：
    /// - true：使用仿真设备，不连接真实硬件（会断开现有连接）
    /// - false：连接真实数递鸟摆轮设备（会建立新连接）
    /// 
    /// 切换后立即生效，系统会自动执行连接/断连操作。
    /// </remarks>
    [HttpPost("simulation")]
    [SwaggerOperation(
        Summary = "切换仿真模式",
        Description = "切换数递鸟摆轮设备的仿真模式，用于测试和实际运行切换。切换时会自动执行连接/断连操作。",
        OperationId = "ToggleShuDiNiaoSimulation",
        Tags = new[] { "数递鸟摆轮配置" }
    )]
    [SwaggerResponse(200, "切换成功", typeof(WheelDiverterConfiguration))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(WheelDiverterConfiguration), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<WheelDiverterConfiguration>> ToggleSimulation(
        [FromQuery, Required] bool useSimulation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取当前配置
            var config = _repository.Get();

            if (config.ShuDiNiao == null)
            {
                return BadRequest(new { message = "未配置数递鸟摆轮设备" });
            }

            // 更新仿真模式
            config.ShuDiNiao = config.ShuDiNiao with { UseSimulation = useSimulation };

            // 保存配置
            _repository.Update(config);

            _logger.LogInformation(
                "数递鸟摆轮仿真模式已切换: UseSimulation={UseSimulation}",
                useSimulation);

            // 应用热更新：根据仿真模式切换连接状态
            if (_driverManager != null)
            {
                if (useSimulation)
                {
                    // 切换到仿真模式，断开所有真实连接
                    await _driverManager.DisconnectAllAsync(cancellationToken);
                    _logger.LogInformation("已断开所有数递鸟摆轮真实连接（切换到仿真模式）");
                }
                else
                {
                    // 切换到真实模式，应用配置重建连接
                    var applyResult = await _driverManager.ApplyConfigurationAsync(config, cancellationToken);
                    
                    if (applyResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "数递鸟摆轮驱动器已重连: 已连接={ConnectedCount}/{TotalCount}",
                            applyResult.ConnectedCount,
                            applyResult.TotalCount);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "数递鸟摆轮驱动器重连部分失败: {ErrorMessage}",
                            applyResult.ErrorMessage);
                    }
                }
            }

            // 重新获取更新后的配置
            var updatedConfig = _repository.Get();
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换数递鸟摆轮仿真模式失败");
            return StatusCode(500, new { message = "切换数递鸟摆轮仿真模式失败" });
        }
    }

    /// <summary>
    /// 测试摆轮转向（调试/测试用）
    /// </summary>
    /// <param name="request">测试请求，包含摆轮ID列表和转向方向</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>测试执行结果</returns>
    /// <response code="200">测试执行成功</response>
    /// <response code="400">请求参数无效或驱动管理器未注册</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **此接口用于测试/调试摆轮转向功能，在系统未启动或故障状态下也可用**
    /// 
    /// 示例请求（测试单个摆轮）:
    /// 
    ///     POST /api/config/wheeldiverter/shudiniao/test
    ///     {
    ///         "diverterIds": ["D1"],
    ///         "direction": "Left"
    ///     }
    /// 
    /// 示例请求（测试多个摆轮）:
    /// 
    ///     POST /api/config/wheeldiverter/shudiniao/test
    ///     {
    ///         "diverterIds": ["D1", "D2", "D3"],
    ///         "direction": "Right"
    ///     }
    /// 
    /// **支持的方向**:
    /// - Left: 左转
    /// - Right: 右转
    /// - Straight: 直行/回中
    /// 
    /// **注意事项**:
    /// - 此接口在任何系统状态下都可用（包括未启动、故障等状态）
    /// - 用于验证摆轮硬件连接和控制是否正常
    /// - 每个摆轮的执行结果会单独返回
    /// </remarks>
    [HttpPost("test")]
    [SwaggerOperation(
        Summary = "测试摆轮转向（任何状态可用）",
        Description = "测试指定摆轮的转向功能，可同时测试多个摆轮。在系统未启动或故障状态下也可用。",
        OperationId = "TestShuDiNiaoDiverters",
        Tags = new[] { "数递鸟摆轮配置" }
    )]
    [SwaggerResponse(200, "测试执行成功", typeof(WheelDiverterTestResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(WheelDiverterTestResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<WheelDiverterTestResponse>> TestDiverters(
        [FromBody] WheelDiverterTestRequest request,
        CancellationToken cancellationToken = default)
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

            if (request.DiverterIds == null || !request.DiverterIds.Any())
            {
                return BadRequest(new { message = "摆轮ID列表不能为空" });
            }

            if (_driverManager == null)
            {
                return BadRequest(new { message = "摆轮驱动管理器未注册，可能是仿真模式或系统启动阶段" });
            }

            var results = new List<WheelDiverterTestResult>();
            var activeDrivers = _driverManager.GetActiveDrivers();

            foreach (var diverterId in request.DiverterIds)
            {
                WheelDiverterTestResult result;

                try
                {
                    if (!activeDrivers.TryGetValue(diverterId, out var driver))
                    {
                        result = new WheelDiverterTestResult
                        {
                            DiverterId = diverterId,
                            Direction = request.Direction,
                            IsSuccess = false,
                            Message = $"未找到摆轮 {diverterId} 的驱动器"
                        };
                        results.Add(result);
                        continue;
                    }

                    bool operationSuccess = request.Direction switch
                    {
                        DiverterDirection.Left => await driver.TurnLeftAsync(cancellationToken),
                        DiverterDirection.Right => await driver.TurnRightAsync(cancellationToken),
                        DiverterDirection.Straight => await driver.PassThroughAsync(cancellationToken),
                        _ => false
                    };

                    result = new WheelDiverterTestResult
                    {
                        DiverterId = diverterId,
                        Direction = request.Direction,
                        IsSuccess = operationSuccess,
                        Message = operationSuccess 
                            ? $"摆轮 {diverterId} 已执行 {request.Direction} 操作" 
                            : $"摆轮 {diverterId} 执行 {request.Direction} 操作失败"
                    };

                    _logger.LogInformation(
                        "测试摆轮转向: DiverterId={DiverterId}, Direction={Direction}, Success={Success}",
                        diverterId, request.Direction, operationSuccess);
                }
                catch (Exception ex)
                {
                    result = new WheelDiverterTestResult
                    {
                        DiverterId = diverterId,
                        Direction = request.Direction,
                        IsSuccess = false,
                        Message = $"摆轮 {diverterId} 执行异常: {ex.Message}"
                    };
                    _logger.LogError(ex, "测试摆轮转向异常: DiverterId={DiverterId}", diverterId);
                }

                results.Add(result);
            }

            var response = new WheelDiverterTestResponse
            {
                TotalCount = results.Count,
                SuccessCount = results.Count(r => r.IsSuccess),
                FailedCount = results.Count(r => !r.IsSuccess),
                Results = results
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试数递鸟摆轮转向失败");
            return StatusCode(500, new { message = "测试摆轮转向失败" });
        }
    }
}

/// <summary>
/// 更新数递鸟摆轮配置请求
/// </summary>
public record class UpdateShuDiNiaoConfigRequest
{
    /// <summary>
    /// 数递鸟摆轮设备列表
    /// </summary>
    [Required(ErrorMessage = "设备列表不能为空")]
    public required List<ShuDiNiaoDeviceRequest> Devices { get; init; }

    /// <summary>
    /// 是否启用仿真模式
    /// </summary>
    public bool UseSimulation { get; init; } = false;
}

/// <summary>
/// 数递鸟摆轮设备请求
/// </summary>
public record class ShuDiNiaoDeviceRequest
{
    /// <summary>
    /// 摆轮标识符
    /// </summary>
    /// <example>D1</example>
    [Required(ErrorMessage = "摆轮标识符不能为空")]
    [StringLength(50, ErrorMessage = "摆轮标识符长度不能超过50个字符")]
    public required string DiverterId { get; init; }

    /// <summary>
    /// TCP连接主机地址
    /// </summary>
    /// <example>192.168.0.100</example>
    [Required(ErrorMessage = "主机地址不能为空")]
    [StringLength(255, ErrorMessage = "主机地址长度不能超过255个字符")]
    public required string Host { get; init; }

    /// <summary>
    /// TCP连接端口
    /// </summary>
    /// <example>2000</example>
    [Range(1, 65535, ErrorMessage = "端口号必须在1到65535之间")]
    public required int Port { get; init; }

    /// <summary>
    /// 设备地址（协议中的设备地址字节）
    /// </summary>
    /// <remarks>
    /// 81 (0x51) 表示 1 号设备
    /// 82 (0x52) 表示 2 号设备
    /// 依次类推
    /// </remarks>
    /// <example>81</example>
    [Range(0x51, 0xFF, ErrorMessage = "设备地址必须在0x51到0xFF之间")]
    public required byte DeviceAddress { get; init; }

    /// <summary>
    /// 左转方向对应的格口ID
    /// </summary>
    /// <remarks>
    /// 摆轮向左转时，包裹将被分流到此格口。
    /// 如果为null，表示左侧没有格口。
    /// </remarks>
    /// <example>1</example>
    public int? LeftChuteId { get; init; }

    /// <summary>
    /// 右转方向对应的格口ID
    /// </summary>
    /// <remarks>
    /// 摆轮向右转时，包裹将被分流到此格口。
    /// 如果为null，表示右侧没有格口。
    /// </remarks>
    /// <example>2</example>
    public int? RightChuteId { get; init; }

    /// <summary>
    /// 是否启用该设备
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// 摆轮测试请求
/// </summary>
/// <remarks>
/// 用于测试数递鸟摆轮转向功能，可同时测试多个摆轮。
/// 此接口在系统未启动或故障状态下也可用，用于验证硬件连接。
/// </remarks>
public record class WheelDiverterTestRequest
{
    /// <summary>
    /// 摆轮ID列表
    /// </summary>
    /// <remarks>
    /// 要测试的摆轮设备ID列表，可以同时测试多个摆轮。
    /// ID需与配置中的DiverterId一致。
    /// </remarks>
    /// <example>["D1", "D2"]</example>
    [Required(ErrorMessage = "摆轮ID列表不能为空")]
    public required List<string> DiverterIds { get; init; }

    /// <summary>
    /// 转向方向
    /// </summary>
    /// <remarks>
    /// 支持的方向：
    /// - Straight (0): 直行/回中 - 让包裹直行通过
    /// - Left (1): 左转 - 将包裹分流到左侧格口
    /// - Right (2): 右转 - 将包裹分流到右侧格口
    /// </remarks>
    /// <example>Left</example>
    [Required(ErrorMessage = "转向方向不能为空")]
    public required DiverterDirection Direction { get; init; }
}

/// <summary>
/// 摆轮测试响应
/// </summary>
/// <remarks>
/// 包含测试执行的汇总信息和每个摆轮的详细结果。
/// </remarks>
public record class WheelDiverterTestResponse
{
    /// <summary>
    /// 测试摆轮总数
    /// </summary>
    /// <example>3</example>
    public int TotalCount { get; init; }

    /// <summary>
    /// 成功数量
    /// </summary>
    /// <example>2</example>
    public int SuccessCount { get; init; }

    /// <summary>
    /// 失败数量
    /// </summary>
    /// <example>1</example>
    public int FailedCount { get; init; }

    /// <summary>
    /// 各摆轮测试结果详情
    /// </summary>
    public required List<WheelDiverterTestResult> Results { get; init; }
}

/// <summary>
/// 单个摆轮测试结果
/// </summary>
public record class WheelDiverterTestResult
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    /// <example>D1</example>
    public required string DiverterId { get; init; }

    /// <summary>
    /// 测试的转向方向
    /// </summary>
    /// <example>Left</example>
    public DiverterDirection Direction { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    /// <example>true</example>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 结果消息
    /// </summary>
    /// <example>摆轮 D1 已执行 Left 操作</example>
    public string? Message { get; init; }
}
