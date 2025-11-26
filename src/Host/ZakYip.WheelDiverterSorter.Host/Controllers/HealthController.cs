using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Observability.Runtime.Health;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 健康检查API控制器
/// </summary>
/// <remarks>
/// 提供进程级和线体级的健康检查端点，支持Kubernetes等容器编排平台的健康探测
/// </remarks>
[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly ISystemStateManager _stateManager;
    private readonly IHealthStatusProvider _healthStatusProvider;
    private readonly IPreRunHealthCheckService? _preRunHealthCheckService;
    private readonly ISystemClock _systemClock;
    private readonly ILogger<HealthController> _logger;
    private readonly ISimulationModeProvider _simulationModeProvider;
    private readonly IWheelDiverterDriverManager? _wheelDiverterDriverManager;
    private readonly IWheelDiverterConfigurationRepository? _wheelDiverterConfigRepository;
    private readonly IDriverConfigurationRepository? _ioDriverConfigRepository;

    public HealthController(
        ISystemStateManager stateManager,
        IHealthStatusProvider healthStatusProvider,
        ISystemClock systemClock,
        ILogger<HealthController> logger,
        ISimulationModeProvider simulationModeProvider,
        IPreRunHealthCheckService? preRunHealthCheckService = null,
        IWheelDiverterDriverManager? wheelDiverterDriverManager = null,
        IWheelDiverterConfigurationRepository? wheelDiverterConfigRepository = null,
        IDriverConfigurationRepository? ioDriverConfigRepository = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _healthStatusProvider = healthStatusProvider ?? throw new ArgumentNullException(nameof(healthStatusProvider));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _preRunHealthCheckService = preRunHealthCheckService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _simulationModeProvider = simulationModeProvider ?? throw new ArgumentNullException(nameof(simulationModeProvider));
        _wheelDiverterDriverManager = wheelDiverterDriverManager;
        _wheelDiverterConfigRepository = wheelDiverterConfigRepository;
        _ioDriverConfigRepository = ioDriverConfigRepository;
    }

    /// <summary>
    /// 系统状态查询端点（支持高并发）
    /// </summary>
    /// <returns>当前系统状态和运行环境模式</returns>
    /// <response code="200">查询成功</response>
    /// <remarks>
    /// 用于快速查询系统当前状态和运行环境，支持超高并发查询（可能100ms会调用一次）。
    /// 此端点设计为轻量级，返回以下信息：
    /// 
    /// - **SystemState**: 当前系统状态（Ready/Running/Paused/Faulted/EmergencyStop/Booting）
    /// - **SystemStateDisplayName**: 状态的中文显示名称
    /// - **EnvironmentMode**: 运行环境模式
    ///   - "Production": 正式环境（使用真实硬件驱动）
    ///   - "Simulation": 仿真环境（使用模拟驱动）
    /// - **IsAbnormal**: 是否处于异常状态（Faulted 或 EmergencyStop 时为 true）
    /// - **ErrorReason**: 异常原因（中文），仅在异常状态时有值
    /// 
    /// 此端点适用于：
    /// - 前端UI实时状态显示
    /// - 监控系统定时轮询
    /// - 负载均衡器健康检查（Kubernetes liveness probe）
    /// - 第三方系统集成
    /// </remarks>
    [HttpGet("api/system/status")]
    [SwaggerOperation(
        Summary = "查询系统状态和环境模式",
        Description = "快速查询系统当前状态和运行环境（正式/仿真），支持高并发查询。异常状态时返回中文异常原因。",
        OperationId = "GetSystemStatus",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "查询成功", typeof(SystemStatusResponse))]
    [ProducesResponseType(typeof(SystemStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetSystemStatus()
    {
        var currentState = _stateManager.CurrentState;
        var isSimulation = _simulationModeProvider.IsSimulationMode();
        
        // 判断是否异常状态
        var isAbnormal = currentState == SystemState.Faulted || currentState == SystemState.EmergencyStop;
        
        // 获取异常原因（中文）
        string? errorReason = null;
        if (isAbnormal)
        {
            errorReason = GetSystemStateErrorReason(currentState);
        }
        
        var response = new SystemStatusResponse
        {
            SystemState = currentState,
            SystemStateDisplayName = GetSystemStateDisplayName(currentState),
            EnvironmentMode = isSimulation ? EnvironmentMode.Simulation : EnvironmentMode.Production,
            IsAbnormal = isAbnormal,
            ErrorReason = errorReason,
            Timestamp = _systemClock.LocalNowOffset
        };

        return Ok(response);
    }

    /// <summary>
    /// 获取系统状态的中文显示名称
    /// </summary>
    private static string GetSystemStateDisplayName(SystemState state)
    {
        return state switch
        {
            SystemState.Booting => "启动中",
            SystemState.Ready => "就绪",
            SystemState.Running => "运行中",
            SystemState.Paused => "暂停",
            SystemState.Faulted => "故障",
            SystemState.EmergencyStop => "急停",
            _ => state.ToString()
        };
    }

    /// <summary>
    /// 获取异常状态的中文原因描述
    /// </summary>
    private string GetSystemStateErrorReason(SystemState state)
    {
        // 尝试从最近的状态转移历史获取错误原因
        var transitionHistory = _stateManager.GetTransitionHistory(5);
        var lastFailedTransition = transitionHistory
            .Where(t => t.ToState == state && !string.IsNullOrEmpty(t.FailureReason))
            .LastOrDefault();

        if (lastFailedTransition != null && !string.IsNullOrEmpty(lastFailedTransition.FailureReason))
        {
            return lastFailedTransition.FailureReason;
        }

        // 如果没有具体的失败原因，返回默认描述
        return state switch
        {
            SystemState.Faulted => "系统发生故障，请检查系统日志或联系管理员",
            SystemState.EmergencyStop => "系统处于急停状态，请检查急停按钮是否被按下，确认安全后解除急停",
            _ => "系统处于异常状态"
        };
    }

    /// <summary>
    /// 就绪状态健康检查端点（Kubernetes readiness probe）
    /// </summary>
    /// <returns>详细的系统健康状态</returns>
    /// <response code="200">系统就绪，可接收流量</response>
    /// <response code="503">系统未就绪</response>
    /// <remarks>
    /// 用于容器编排平台的就绪检查（readiness probe）。
    /// 返回详细的健康检查结果，包括：
    /// 
    /// 关键模块健康状态：
    /// - RuleEngine 连接状态（push模型）
    /// - TTL 调度线程状态（TODO: 待实现）
    /// - 驱动器健康状态
    /// - 传感器健康状态
    /// - 系统状态（Ready/Running/Faulted等）
    /// - 自检结果
    /// - 降级模式和降级节点信息
    /// - 近期Critical告警统计
    /// 
    /// 状态码说明：
    /// - 200 OK: 系统状态为Ready/Running且所有关键模块健康
    /// - 503 ServiceUnavailable: 系统故障或关键模块不健康
    /// </remarks>
    [HttpGet("health/ready")]
    [SwaggerOperation(
        Summary = "就绪状态健康检查（Readiness）",
        Description = "返回详细的健康检查结果，包括RuleEngine连接、TTL调度、驱动器等关键模块状态",
        OperationId = "GetReadinessHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "系统就绪", typeof(LineHealthResponse))]
    [SwaggerResponse(503, "系统未就绪", typeof(LineHealthResponse))]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LineHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadinessHealth()
    {
        try
        {
            // 使用 IHealthStatusProvider 获取健康快照
            var snapshot = await _healthStatusProvider.GetHealthSnapshotAsync();

            // 获取运行环境模式
            var isSimulation = _simulationModeProvider.IsSimulationMode();

            // 映射到响应DTO
            var response = MapSnapshotToResponse(snapshot, isSimulation);

            // 判断HTTP状态码：检查所有关键模块
            var isReady = snapshot.IsLineAvailable && snapshot.IsSelfTestSuccess;
            
            // 检查 RuleEngine 连接状态
            var ruleEngineHealthy = snapshot.Upstreams?.All(u => u.IsHealthy) ?? true;
            
            // 检查驱动器状态
            var driversHealthy = snapshot.Drivers?.All(d => d.IsHealthy) ?? true;
            
            isReady = isReady && ruleEngineHealthy && driversHealthy;

            if (isReady)
            {
                return Ok(response);
            }
            else
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "就绪状态健康检查失败");
            var response = new LineHealthResponse
            {
                SystemState = SystemState.Faulted,
                IsSelfTestSuccess = false,
                LastSelfTestAt = null
            };
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }

    /// <summary>
    /// 运行前健康检查端点（Pre-Run Check）
    /// </summary>
    /// <returns>运行前检查结果</returns>
    /// <response code="200">系统配置完整，可以开始运行</response>
    /// <response code="503">系统配置不完整，不可运行</response>
    /// <remarks>
    /// 在开始实际分拣/仿真前验证系统基础运行条件：
    /// 
    /// 检查项目：
    /// - 异常口配置是否正确且存在于拓扑中
    /// - 启动/停止/急停/面板灯等 IO 是否全部配置完毕
    /// - 摆轮拓扑是否完整（至少有一条从入口到首个摆轮的有效路径）
    /// - 线体拓扑中线体长度和线速度是否配置完整且合法
    /// - 上游连接是否已配置
    /// - 其他关键基础运行条件是否满足
    /// 
    /// 返回结构化 JSON，包含 overallStatus 与逐项检查状态，所有 message 为中文描述。
    /// 用于运维/工程在上线前确认系统"具备基本可运行条件"。
    /// </remarks>
    [HttpGet("health/prerun")]
    [SwaggerOperation(
        Summary = "运行前健康检查（Pre-Run Check）",
        Description = "验证系统基础运行条件是否就绪，包括异常口、面板IO、拓扑、线体段配置、上游连接等",
        OperationId = "GetPreRunHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "系统配置完整，可以开始运行", typeof(PreRunHealthCheckResponse))]
    [SwaggerResponse(503, "系统配置不完整，不可运行", typeof(PreRunHealthCheckResponse))]
    [ProducesResponseType(typeof(PreRunHealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PreRunHealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPreRunHealth()
    {
        try
        {
            if (_preRunHealthCheckService == null)
            {
                _logger.LogWarning("PreRunHealthCheckService 未注册，返回服务不可用");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new PreRunHealthCheckResponse
                {
                    OverallStatus = HealthStatus.Unhealthy,
                    Checks = new List<HealthCheckItemResponse>
                    {
                        new HealthCheckItemResponse
                        {
                            Name = "ServiceAvailability",
                            Status = HealthStatus.Unhealthy,
                            Message = "运行前健康检查服务未启用"
                        }
                    }
                });
            }

            var result = await _preRunHealthCheckService.ExecuteAsync(HttpContext.RequestAborted);

            var response = new PreRunHealthCheckResponse
            {
                OverallStatus = result.OverallStatus,
                Checks = result.Checks.Select(c => new HealthCheckItemResponse
                {
                    Name = c.Name,
                    Status = c.Status,
                    Message = c.Message
                }).ToList()
            };

            if (result.IsHealthy)
            {
                return Ok(response);
            }
            else
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "运行前健康检查失败");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new PreRunHealthCheckResponse
            {
                OverallStatus = HealthStatus.Unhealthy,
                Checks = new List<HealthCheckItemResponse>
                {
                    new HealthCheckItemResponse
                    {
                        Name = "PreRunCheck",
                        Status = HealthStatus.Unhealthy,
                        Message = "运行前健康检查执行时发生异常"
                    }
                }
            });
        }
    }

    /// <summary>
    /// 驱动器健康检查端点
    /// </summary>
    /// <returns>驱动器健康状态</returns>
    /// <response code="200">所有驱动器健康</response>
    /// <response code="503">部分或全部驱动器不健康</response>
    /// <remarks>
    /// 返回所有驱动器的详细健康状态，包括：
    /// 
    /// **IO驱动器状态**：
    /// - 驱动器类型（IO）
    /// - 厂商类型（Leadshine/Siemens等）
    /// - 是否使用硬件模式
    /// - 连接状态
    /// 
    /// **摆轮驱动器状态**：
    /// - 驱动器类型（WheelDiverter）
    /// - 厂商类型（ShuDiNiao/Modi等）
    /// - 各设备连接状态
    /// - 仿真模式状态
    /// </remarks>
    [HttpGet("health/drivers")]
    [SwaggerOperation(
        Summary = "驱动器健康检查",
        Description = "返回IO驱动器和摆轮驱动器的实时连接状态和健康详情",
        OperationId = "GetDriversHealth",
        Tags = new[] { "健康检查" }
    )]
    [SwaggerResponse(200, "所有驱动器健康", typeof(DriversHealthResponse))]
    [SwaggerResponse(503, "部分或全部驱动器不健康", typeof(DriversHealthResponse))]
    [ProducesResponseType(typeof(DriversHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DriversHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDriversHealth()
    {
        try
        {
            var drivers = new List<DriverHealthInfo>();
            var now = new DateTimeOffset(_systemClock.LocalNow);
            
            // 1. 获取IO驱动器状态
            if (_ioDriverConfigRepository != null)
            {
                try
                {
                    var ioConfig = _ioDriverConfigRepository.Get();
                    var ioVendorDisplayName = GetIoVendorDisplayName(ioConfig.VendorType);
                    var isSimulationMode = !ioConfig.UseHardwareDriver;
                    
                    // 检查配置是否完整（非仿真模式下需要）
                    var isConfigured = true;
                    var configError = string.Empty;
                    if (!isSimulationMode)
                    {
                        if (ioConfig.VendorType == DriverVendorType.Leadshine && ioConfig.Leadshine == null)
                        {
                            isConfigured = false;
                            configError = "雷赛控制卡参数未配置";
                        }
                    }
                    
                    // 非仿真模式下，未配置或未连接都是不健康的
                    var isHealthy = isSimulationMode || (isConfigured);
                    
                    var ioDriverStatus = new DriverHealthInfo
                    {
                        DriverName = $"IO驱动器 ({ioVendorDisplayName})",
                        DriverType = DriverCategory.IoDriver,
                        VendorType = ioConfig.VendorType.ToString(),
                        VendorDisplayName = ioVendorDisplayName,
                        IsConnected = !isSimulationMode && isConfigured,
                        IsSimulationMode = isSimulationMode,
                        IsHealthy = isHealthy,
                        ErrorCode = !isHealthy ? "NOT_CONFIGURED" : null,
                        ErrorMessage = isSimulationMode 
                            ? "仿真模式运行中" 
                            : (isConfigured 
                                ? $"硬件模式已启用，厂商: {ioVendorDisplayName}" 
                                : configError),
                        CheckedAt = now
                    };
                    drivers.Add(ioDriverStatus);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取IO驱动器配置失败");
                    drivers.Add(new DriverHealthInfo
                    {
                        DriverName = "IO驱动器",
                        DriverType = DriverCategory.IoDriver,
                        IsHealthy = false,
                        IsConnected = false,
                        IsSimulationMode = false,
                        ErrorCode = "CONFIG_ERROR",
                        ErrorMessage = "无法获取IO驱动器配置",
                        CheckedAt = now
                    });
                }
            }
            else
            {
                // 未注入IO驱动器配置仓储时，不健康
                drivers.Add(new DriverHealthInfo
                {
                    DriverName = "IO驱动器",
                    DriverType = DriverCategory.IoDriver,
                    IsHealthy = false,
                    IsConnected = false,
                    IsSimulationMode = false,
                    ErrorCode = "NOT_CONFIGURED",
                    ErrorMessage = "IO驱动器配置服务未初始化",
                    CheckedAt = now
                });
            }
            
            // 2. 获取摆轮驱动器状态
            if (_wheelDiverterConfigRepository != null)
            {
                try
                {
                    var wheelConfig = _wheelDiverterConfigRepository.Get();
                    var wheelDriversAdded = false;
                    
                    // 根据厂商类型获取设备状态
                    switch (wheelConfig.VendorType)
                    {
                        case WheelDiverterVendorType.ShuDiNiao:
                            if (wheelConfig.ShuDiNiao != null)
                            {
                                var isSimulation = wheelConfig.ShuDiNiao.UseSimulation;
                                var activeDrivers = _wheelDiverterDriverManager?.GetActiveDrivers() 
                                    ?? new Dictionary<string, IWheelDiverterDriver>();
                                var enabledDevices = wheelConfig.ShuDiNiao.Devices.Where(d => d.IsEnabled).ToList();
                                
                                if (enabledDevices.Count > 0)
                                {
                                    wheelDriversAdded = true;
                                    foreach (var device in enabledDevices)
                                    {
                                        var isConnected = activeDrivers.ContainsKey(device.DiverterId.ToString());
                                        // 非仿真模式下，未连接是不健康的
                                        var isHealthy = isSimulation || isConnected;
                                        drivers.Add(new DriverHealthInfo
                                        {
                                            DriverName = $"摆轮驱动器 {device.DiverterId} (数递鸟)",
                                            DriverType = DriverCategory.WheelDiverter,
                                            VendorType = "ShuDiNiao",
                                            VendorDisplayName = "数递鸟",
                                            IsConnected = isSimulation ? false : isConnected,
                                            IsSimulationMode = isSimulation,
                                            IsHealthy = isHealthy,
                                            ErrorCode = (!isSimulation && !isConnected) ? "DISCONNECTED" : null,
                                            ErrorMessage = isSimulation 
                                                ? "仿真模式运行中" 
                                                : (isConnected ? $"已连接 ({device.Host}:{device.Port})" : $"未连接 ({device.Host}:{device.Port})"),
                                            CheckedAt = now
                                        });
                                    }
                                }
                            }
                            break;
                            
                        case WheelDiverterVendorType.Modi:
                            if (wheelConfig.Modi != null)
                            {
                                var isSimulation = wheelConfig.Modi.UseSimulation;
                                var activeDrivers = _wheelDiverterDriverManager?.GetActiveDrivers() 
                                    ?? new Dictionary<string, IWheelDiverterDriver>();
                                var enabledDevices = wheelConfig.Modi.Devices.Where(d => d.IsEnabled).ToList();
                                
                                if (enabledDevices.Count > 0)
                                {
                                    wheelDriversAdded = true;
                                    foreach (var device in enabledDevices)
                                    {
                                        var isConnected = activeDrivers.ContainsKey(device.DiverterId.ToString());
                                        // 非仿真模式下，未连接是不健康的
                                        var isHealthy = isSimulation || isConnected;
                                        drivers.Add(new DriverHealthInfo
                                        {
                                            DriverName = $"摆轮驱动器 {device.DiverterId} (莫迪)",
                                            DriverType = DriverCategory.WheelDiverter,
                                            VendorType = "Modi",
                                            VendorDisplayName = "莫迪",
                                            IsConnected = isSimulation ? false : isConnected,
                                            IsSimulationMode = isSimulation,
                                            IsHealthy = isHealthy,
                                            ErrorCode = (!isSimulation && !isConnected) ? "DISCONNECTED" : null,
                                            ErrorMessage = isSimulation 
                                                ? "仿真模式运行中" 
                                                : (isConnected ? $"已连接 ({device.Host}:{device.Port})" : $"未连接 ({device.Host}:{device.Port})"),
                                            CheckedAt = now
                                        });
                                    }
                                }
                            }
                            break;
                    }
                    
                    // 如果没有添加任何摆轮驱动器条目，添加一个汇总状态
                    if (!wheelDriversAdded)
                    {
                        var vendorDisplayName = GetWheelDiverterVendorDisplayName(wheelConfig.VendorType);
                        drivers.Add(new DriverHealthInfo
                        {
                            DriverName = $"摆轮驱动器 ({vendorDisplayName})",
                            DriverType = DriverCategory.WheelDiverter,
                            VendorType = wheelConfig.VendorType.ToString(),
                            VendorDisplayName = vendorDisplayName,
                            IsHealthy = true,
                            IsConnected = false,
                            IsSimulationMode = true,
                            ErrorCode = null,
                            ErrorMessage = "未配置启用的摆轮设备",
                            CheckedAt = now
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取摆轮驱动器配置失败");
                    drivers.Add(new DriverHealthInfo
                    {
                        DriverName = "摆轮驱动器",
                        DriverType = DriverCategory.WheelDiverter,
                        IsHealthy = false,
                        IsConnected = false,
                        ErrorCode = "CONFIG_ERROR",
                        ErrorMessage = "无法获取摆轮驱动器配置",
                        CheckedAt = now
                    });
                }
            }
            else
            {
                // 未注入摆轮驱动器配置仓储时，添加提示信息
                drivers.Add(new DriverHealthInfo
                {
                    DriverName = "摆轮驱动器",
                    DriverType = DriverCategory.WheelDiverter,
                    IsHealthy = true,
                    IsConnected = false,
                    IsSimulationMode = true,
                    ErrorCode = null,
                    ErrorMessage = "摆轮驱动器配置服务未初始化",
                    CheckedAt = now
                });
            }
            
            // 如果没有任何驱动器信息，从健康快照中获取（向后兼容）
            if (drivers.Count == 0)
            {
                var snapshot = await _healthStatusProvider.GetHealthSnapshotAsync();
                drivers = snapshot.Drivers?.Select(d => new DriverHealthInfo
                {
                    DriverName = d.DriverName,
                    IsHealthy = d.IsHealthy,
                    ErrorCode = d.ErrorCode,
                    ErrorMessage = d.ErrorMessage,
                    CheckedAt = d.CheckedAt
                }).ToList() ?? new List<DriverHealthInfo>();
            }

            var allHealthy = drivers.Count == 0 || drivers.All(d => d.IsHealthy);

            var response = new DriversHealthResponse
            {
                OverallStatus = allHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                TotalDrivers = drivers.Count,
                HealthyDrivers = drivers.Count(d => d.IsHealthy),
                UnhealthyDrivers = drivers.Count(d => !d.IsHealthy),
                Drivers = drivers,
                CheckedAt = now
            };

            if (allHealthy)
            {
                return Ok(response);
            }
            else
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驱动器健康检查失败");
            var response = new DriversHealthResponse
            {
                OverallStatus = HealthStatus.Unknown,
                TotalDrivers = 0,
                HealthyDrivers = 0,
                UnhealthyDrivers = 0,
                Drivers = new List<DriverHealthInfo>(),
                CheckedAt = new DateTimeOffset(_systemClock.LocalNow)
            };
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }

    private static LineHealthResponse MapSnapshotToResponse(LineHealthSnapshot snapshot, bool isSimulation)
    {
        return new LineHealthResponse
        {
            SystemState = snapshot.SystemState,
            EnvironmentMode = isSimulation ? EnvironmentMode.Simulation : EnvironmentMode.Production,
            IsSelfTestSuccess = snapshot.IsSelfTestSuccess,
            LastSelfTestAt = snapshot.LastSelfTestAt,
            Drivers = snapshot.Drivers?.Select(d => new DriverHealthInfo
            {
                DriverName = d.DriverName,
                IsHealthy = d.IsHealthy,
                ErrorCode = d.ErrorCode,
                ErrorMessage = d.ErrorMessage,
                CheckedAt = d.CheckedAt
            }).ToList(),
            Upstreams = snapshot.Upstreams?.Select(u => new UpstreamHealthInfo
            {
                EndpointName = u.EndpointName,
                IsHealthy = u.IsHealthy,
                ErrorCode = u.ErrorCode,
                ErrorMessage = u.ErrorMessage,
                CheckedAt = u.CheckedAt
            }).ToList(),
            Config = snapshot.Config != null ? new ConfigHealthInfo
            {
                IsValid = snapshot.Config.IsValid,
                ErrorMessage = snapshot.Config.ErrorMessage
            } : null,
            Summary = new SystemSummary
            {
                CurrentCongestionLevel = snapshot.CurrentCongestionLevel,
                RecommendedCapacityParcelsPerMinute = null
            },
            DegradationMode = snapshot.DegradationMode,
            DegradedNodesCount = snapshot.DegradedNodesCount,
            DegradedNodes = snapshot.DegradedNodes?.Select(n => new NodeHealthInfo
            {
                NodeId = n.NodeId,
                NodeType = n.NodeType,
                IsHealthy = n.IsHealthy,
                ErrorCode = n.ErrorCode,
                ErrorMessage = n.ErrorMessage,
                CheckedAt = n.CheckedAt
            }).ToList(),
            DiagnosticsLevel = snapshot.DiagnosticsLevel,
            ConfigVersion = snapshot.ConfigVersion,
            RecentCriticalAlerts = null // 可以从 AlertHistoryService 获取，但快照中已有计数
        };
    }

    /// <summary>
    /// 获取IO驱动厂商显示名称
    /// </summary>
    private static string GetIoVendorDisplayName(DriverVendorType vendorType)
    {
        return vendorType switch
        {
            DriverVendorType.Mock => "模拟驱动",
            DriverVendorType.Leadshine => "雷赛",
            DriverVendorType.Siemens => "西门子",
            DriverVendorType.Mitsubishi => "三菱",
            DriverVendorType.Omron => "欧姆龙",
            _ => vendorType.ToString()
        };
    }

    /// <summary>
    /// 获取摆轮驱动厂商显示名称
    /// </summary>
    private static string GetWheelDiverterVendorDisplayName(WheelDiverterVendorType vendorType)
    {
        return vendorType switch
        {
            WheelDiverterVendorType.ShuDiNiao => "数递鸟",
            WheelDiverterVendorType.Modi => "莫迪",
            _ => vendorType.ToString()
        };
    }
}

/// <summary>
/// 进程级健康响应
/// </summary>
public class ProcessHealthResponse
{
    /// <summary>健康状态</summary>
    public required HealthStatus Status { get; init; }

    /// <summary>失败原因（如果不健康）</summary>
    public string? Reason { get; init; }

    /// <summary>检查时间</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// 线体级健康响应
/// </summary>
public class LineHealthResponse
{
    /// <summary>系统状态</summary>
    public required SystemState SystemState { get; init; }

    /// <summary>运行环境模式</summary>
    public EnvironmentMode? EnvironmentMode { get; init; }

    /// <summary>自检是否成功</summary>
    public bool IsSelfTestSuccess { get; init; }

    /// <summary>最近一次自检时间</summary>
    public DateTimeOffset? LastSelfTestAt { get; init; }

    /// <summary>驱动器健康状态列表</summary>
    public List<DriverHealthInfo>? Drivers { get; init; }

    /// <summary>上游系统健康状态列表</summary>
    public List<UpstreamHealthInfo>? Upstreams { get; init; }

    /// <summary>配置健康状态</summary>
    public ConfigHealthInfo? Config { get; init; }

    /// <summary>系统概要信息（可选）</summary>
    public SystemSummary? Summary { get; init; }

    /// <summary>降级模式（PR-14：节点级降级）</summary>
    public DegradationMode? DegradationMode { get; init; }

    /// <summary>降级节点数量（PR-14：节点级降级）</summary>
    public int? DegradedNodesCount { get; init; }

    /// <summary>降级节点列表摘要（PR-14：节点级降级）</summary>
    public List<NodeHealthInfo>? DegradedNodes { get; init; }

    /// <summary>当前诊断级别（PR-23：可观测性）</summary>
    public DiagnosticsLevel? DiagnosticsLevel { get; init; }

    /// <summary>配置版本号（PR-23：可观测性）</summary>
    public string? ConfigVersion { get; init; }

    /// <summary>最近Critical告警摘要（PR-23：可观测性）</summary>
    public List<AlertSummary>? RecentCriticalAlerts { get; init; }
}

/// <summary>
/// 驱动器健康信息
/// </summary>
public class DriverHealthInfo
{
    /// <summary>驱动器名称</summary>
    public required string DriverName { get; init; }
    
    /// <summary>驱动器类型（IoDriver/WheelDiverter）</summary>
    public DriverCategory? DriverType { get; init; }
    
    /// <summary>厂商类型（Leadshine/Siemens/ShuDiNiao/Modi等）</summary>
    public string? VendorType { get; init; }
    
    /// <summary>厂商/品牌显示名称（如：数递鸟、莫迪、雷赛等）</summary>
    public string? VendorDisplayName { get; init; }
    
    /// <summary>是否已连接</summary>
    public bool IsConnected { get; init; }
    
    /// <summary>是否处于仿真模式</summary>
    public bool IsSimulationMode { get; init; }
    
    /// <summary>是否健康</summary>
    public bool IsHealthy { get; init; }
    
    /// <summary>错误代码（如果不健康）</summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>错误消息或状态描述</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>检查时间</summary>
    public DateTimeOffset CheckedAt { get; init; }
}

/// <summary>
/// 上游系统健康信息
/// </summary>
public class UpstreamHealthInfo
{
    public required string EndpointName { get; init; }
    public bool IsHealthy { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}

/// <summary>
/// 配置健康信息
/// </summary>
public class ConfigHealthInfo
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 系统概要信息
/// </summary>
public class SystemSummary
{
    /// <summary>当前拥堵级别</summary>
    public CongestionLevel? CurrentCongestionLevel { get; init; }

    /// <summary>推荐产能（包裹/分钟）</summary>
    public double? RecommendedCapacityParcelsPerMinute { get; init; }
}

/// <summary>
/// 节点健康信息（PR-14：节点级降级）
/// </summary>
public class NodeHealthInfo
{
    public long NodeId { get; init; }
    public string? NodeType { get; init; }
    public bool IsHealthy { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}

/// <summary>
/// 告警摘要信息（PR-23：可观测性）
/// </summary>
public class AlertSummary
{
    public required string AlertCode { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset RaisedAt { get; init; }
}

/// <summary>
/// 运行前健康检查响应
/// </summary>
public class PreRunHealthCheckResponse
{
    /// <summary>整体状态</summary>
    public required HealthStatus OverallStatus { get; init; }

    /// <summary>各项检查结果列表</summary>
    public required List<HealthCheckItemResponse> Checks { get; init; }
}

/// <summary>
/// 单项健康检查结果
/// </summary>
public class HealthCheckItemResponse
{
    /// <summary>检查项名称（英文标识符）</summary>
    public required string Name { get; init; }

    /// <summary>检查状态</summary>
    public required HealthStatus Status { get; init; }

    /// <summary>检查结果描述（中文消息）</summary>
    public required string Message { get; init; }
}

/// <summary>
/// 驱动器健康检查响应
/// </summary>
public class DriversHealthResponse
{
    /// <summary>整体状态</summary>
    public required HealthStatus OverallStatus { get; init; }

    /// <summary>驱动器总数</summary>
    public int TotalDrivers { get; init; }

    /// <summary>健康驱动器数量</summary>
    public int HealthyDrivers { get; init; }

    /// <summary>不健康驱动器数量</summary>
    public int UnhealthyDrivers { get; init; }

    /// <summary>驱动器详细信息列表</summary>
    public required List<DriverHealthInfo> Drivers { get; init; }

    /// <summary>检查时间</summary>
    public DateTimeOffset CheckedAt { get; init; }
}
