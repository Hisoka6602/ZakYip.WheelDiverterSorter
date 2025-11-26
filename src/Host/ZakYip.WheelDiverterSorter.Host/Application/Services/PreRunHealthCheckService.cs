// 本文件使用向后兼容API，抑制废弃警告
#pragma warning disable CS0618 // Type or member is obsolete

using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Host.Application.Services;

/// <summary>
/// 运行前健康检查服务实现
/// Pre-run health check service implementation
/// </summary>
public class PreRunHealthCheckService : IPreRunHealthCheckService
{
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly IPanelConfigurationRepository _panelConfigRepository;
    private readonly ILineTopologyRepository _lineTopologyRepository;
    private readonly ICommunicationConfigurationRepository _communicationConfigRepository;
    private readonly IDriverConfigurationRepository _ioDriverConfigRepository;
    private readonly IWheelDiverterConfigurationRepository _wheelDiverterConfigRepository;
    private readonly IWheelDiverterDriverManager? _wheelDiverterDriverManager;
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PreRunHealthCheckService> _logger;

    public PreRunHealthCheckService(
        ISystemConfigurationRepository systemConfigRepository,
        IPanelConfigurationRepository panelConfigRepository,
        ILineTopologyRepository lineTopologyRepository,
        ICommunicationConfigurationRepository communicationConfigRepository,
        IDriverConfigurationRepository ioDriverConfigRepository,
        IWheelDiverterConfigurationRepository wheelDiverterConfigRepository,
        IRuleEngineClient ruleEngineClient,
        ISafeExecutionService safeExecutor,
        ILogger<PreRunHealthCheckService> logger,
        IWheelDiverterDriverManager? wheelDiverterDriverManager = null)
    {
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _lineTopologyRepository = lineTopologyRepository ?? throw new ArgumentNullException(nameof(lineTopologyRepository));
        _communicationConfigRepository = communicationConfigRepository ?? throw new ArgumentNullException(nameof(communicationConfigRepository));
        _ioDriverConfigRepository = ioDriverConfigRepository ?? throw new ArgumentNullException(nameof(ioDriverConfigRepository));
        _wheelDiverterConfigRepository = wheelDiverterConfigRepository ?? throw new ArgumentNullException(nameof(wheelDiverterConfigRepository));
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wheelDiverterDriverManager = wheelDiverterDriverManager;
    }

    /// <inheritdoc />
    public async Task<PreRunHealthCheckResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<HealthCheckItem>();

        // 1. 异常口配置检查
        checks.Add(await CheckExceptionChuteAsync(cancellationToken));

        // 2. 面板 IO 配置检查
        checks.Add(await CheckPanelIoConfigAsync(cancellationToken));

        // 3. 摆轮拓扑完整性检查
        checks.Add(await CheckTopologyCompletenessAsync(cancellationToken));

        // 4. 线体长度与线速度合法性检查
        checks.Add(await CheckLineSegmentsValidityAsync(cancellationToken));

        // 5. 上游连接配置检查
        checks.Add(await CheckUpstreamConnectionConfigAsync(cancellationToken));

        // 6. IO驱动器连接状态检查（新增）
        checks.Add(await CheckIoDriverConnectivityAsync(cancellationToken));

        // 7. 摆轮驱动器连接状态检查（新增）
        checks.Add(await CheckWheelDiverterConnectivityAsync(cancellationToken));

        // 计算整体状态
        var overallStatus = checks.All(c => c.IsHealthy) ? HealthStatus.Healthy : HealthStatus.Unhealthy;

        return new PreRunHealthCheckResult
        {
            OverallStatus = overallStatus,
            Checks = checks
        };
    }

    /// <summary>
    /// 检查异常口配置是否有效
    /// </summary>
    private async Task<HealthCheckItem> CheckExceptionChuteAsync(CancellationToken cancellationToken)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                await Task.Yield();
                var systemConfig = _systemConfigRepository.Get();
                if (systemConfig == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = "系统配置未初始化"
                    };
                }

                var exceptionChuteId = systemConfig.ExceptionChuteId;
                if (exceptionChuteId <= 0)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = "异常口ID未配置或配置为无效值"
                    };
                }

                // 检查异常口是否存在于拓扑中
                var topology = _lineTopologyRepository.Get();
                if (topology == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = "线体拓扑未配置，无法验证异常口"
                    };
                }

                var exceptionChuteExists = topology.Chutes.Any(c =>
                    long.TryParse(c.ChuteId, out var chuteId) && chuteId == exceptionChuteId);

                if (!exceptionChuteExists)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"异常口 {exceptionChuteId} 不存在于线体拓扑中"
                    };
                }

                // 检查异常口是否有有效路径 - 确认格口在拓扑中且可被绑定
                var exceptionChute = topology.FindChuteById(exceptionChuteId.ToString());
                if (exceptionChute == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"异常口 {exceptionChuteId} 无法通过拓扑配置访问"
                    };
                }

                return new HealthCheckItem
                {
                    Name = "ExceptionChuteConfigured",
                    Status = HealthStatus.Healthy,
                    Message = $"异常口 {exceptionChuteId} 已配置且存在于拓扑中"
                };
            },
            operationName: "CheckExceptionChute",
            defaultValue: new HealthCheckItem
            {
                Name = "ExceptionChuteConfigured",
                Status = HealthStatus.Unhealthy,
                Message = "检查异常口配置时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 检查面板 IO 配置是否完整
    /// </summary>
    private async Task<HealthCheckItem> CheckPanelIoConfigAsync(CancellationToken cancellationToken)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                await Task.Yield();
                var panelConfig = _panelConfigRepository.Get();
                if (panelConfig == null || !panelConfig.Enabled)
                {
                    // PR-8: 如果面板未配置或未启用，返回 Unhealthy
                    return new HealthCheckItem
                    {
                        Name = "PanelIoConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = "面板 IO 配置未启用或未配置"
                    };
                }

                var missingConfigs = new List<string>();

                // 检查按钮输入配置
                if (!panelConfig.StartButtonInputBit.HasValue)
                    missingConfigs.Add("开始按钮 IO");
                if (!panelConfig.StopButtonInputBit.HasValue)
                    missingConfigs.Add("停止按钮 IO");
                if (!panelConfig.EmergencyStopButtonInputBit.HasValue)
                    missingConfigs.Add("急停按钮 IO");

                // 检查指示灯输出配置
                if (!panelConfig.StartLightOutputBit.HasValue)
                    missingConfigs.Add("开始按钮灯 IO");
                if (!panelConfig.StopLightOutputBit.HasValue)
                    missingConfigs.Add("停止按钮灯 IO");
                if (!panelConfig.ConnectionLightOutputBit.HasValue)
                    missingConfigs.Add("连接状态灯 IO");

                // 检查三色灯配置
                if (!panelConfig.SignalTowerRedOutputBit.HasValue)
                    missingConfigs.Add("三色灯-红 IO");
                if (!panelConfig.SignalTowerYellowOutputBit.HasValue)
                    missingConfigs.Add("三色灯-黄 IO");
                if (!panelConfig.SignalTowerGreenOutputBit.HasValue)
                    missingConfigs.Add("三色灯-绿 IO");

                if (missingConfigs.Any())
                {
                    return new HealthCheckItem
                    {
                        Name = "PanelIoConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"缺少面板 IO 配置：{string.Join("、", missingConfigs)}"
                    };
                }

                // 验证配置有效性
                (bool isValid, string? errorMessage) = panelConfig.Validate();
                if (!isValid)
                {
                    return new HealthCheckItem
                    {
                        Name = "PanelIoConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"面板配置验证失败：{errorMessage}"
                    };
                }

                return new HealthCheckItem
                {
                    Name = "PanelIoConfigured",
                    Status = HealthStatus.Healthy,
                    Message = "面板 IO 配置完整且有效"
                };
            },
            operationName: "CheckPanelIoConfig",
            defaultValue: new HealthCheckItem
            {
                Name = "PanelIoConfigured",
                Status = HealthStatus.Unhealthy,
                Message = "检查面板 IO 配置时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 检查摆轮拓扑完整性
    /// </summary>
    private async Task<HealthCheckItem> CheckTopologyCompletenessAsync(CancellationToken cancellationToken)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                await Task.Yield();
                var topology = _lineTopologyRepository.Get();
                if (topology == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "LineTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "线体拓扑未配置"
                    };
                }

                // 检查是否有摆轮节点
                if (topology.WheelNodes.Count == 0)
                {
                    return new HealthCheckItem
                    {
                        Name = "LineTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "拓扑中未配置任何摆轮节点"
                    };
                }

                // 检查入口到首个摆轮的路径
                var firstWheel = topology.WheelNodes.OrderBy(n => n.PositionIndex).FirstOrDefault();
                if (firstWheel == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "LineTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "无法找到首个摆轮节点"
                    };
                }

                // 检查是否有线体段配置
                if (topology.LineSegments == null || topology.LineSegments.Count == 0)
                {
                    return new HealthCheckItem
                    {
                        Name = "LineTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "线体段配置为空，无法验证拓扑路径"
                    };
                }

                // 检查每个配置的格口是否绑定到了摆轮节点
                var unboundChutes = new List<string>();
                foreach (var chute in topology.Chutes)
                {
                    var boundNode = topology.FindNodeById(chute.BoundNodeId);
                    if (boundNode == null)
                    {
                        unboundChutes.Add(chute.ChuteId);
                    }
                }

                if (unboundChutes.Any())
                {
                    return new HealthCheckItem
                    {
                        Name = "LineTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = $"以下格口未正确绑定到摆轮节点：{string.Join("、", unboundChutes)}"
                    };
                }

                return new HealthCheckItem
                {
                    Name = "LineTopologyValid",
                    Status = HealthStatus.Healthy,
                    Message = $"拓扑配置完整，共 {topology.WheelNodes.Count} 个摆轮节点，{topology.Chutes.Count} 个格口"
                };
            },
            operationName: "CheckTopologyCompleteness",
            defaultValue: new HealthCheckItem
            {
                Name = "LineTopologyValid",
                Status = HealthStatus.Unhealthy,
                Message = "检查拓扑完整性时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 检查线体段长度与线速度合法性
    /// </summary>
    private async Task<HealthCheckItem> CheckLineSegmentsValidityAsync(CancellationToken cancellationToken)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                await Task.Yield();
                var topology = _lineTopologyRepository.Get();
                if (topology == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "LineSegmentsLengthAndSpeedValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "线体拓扑未配置，无法检查线体段"
                    };
                }

                // 检查是否有线体段配置
                if (topology.LineSegments.Count == 0)
                {
                    return new HealthCheckItem
                    {
                        Name = "LineSegmentsLengthAndSpeedValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "线体段数量为0，必须至少配置一个线体段"
                    };
                }

                var invalidSegments = new List<string>();

                foreach (var segment in topology.LineSegments)
                {
                    if (segment.LengthMm <= 0)
                    {
                        invalidSegments.Add($"{segment.SegmentId}(长度={segment.LengthMm}mm)");
                    }
                    else if (segment.SpeedMmPerSec <= 0)
                    {
                        invalidSegments.Add($"{segment.SegmentId}(速度={segment.SpeedMmPerSec}mm/s)");
                    }
                }

                if (invalidSegments.Any())
                {
                    return new HealthCheckItem
                    {
                        Name = "LineSegmentsLengthAndSpeedValid",
                        Status = HealthStatus.Unhealthy,
                        Message = $"发现 {invalidSegments.Count} 个非法线体段配置：{string.Join("、", invalidSegments)}"
                    };
                }

                // 检查第一个线体段（入口段）是否配置正确
                var firstSegment = topology.LineSegments.FirstOrDefault();
                if (firstSegment != null)
                {
                    if (firstSegment.LengthMm <= 0 || firstSegment.SpeedMmPerSec <= 0)
                    {
                        return new HealthCheckItem
                        {
                            Name = "LineSegmentsLengthAndSpeedValid",
                            Status = HealthStatus.Unhealthy,
                            Message = $"入口线体段 {firstSegment.SegmentId} 配置无效（长度={firstSegment.LengthMm}mm，速度={firstSegment.SpeedMmPerSec}mm/s）"
                        };
                    }
                }

                return new HealthCheckItem
                {
                    Name = "LineSegmentsLengthAndSpeedValid",
                    Status = HealthStatus.Healthy,
                    Message = $"所有 {topology.LineSegments.Count} 个线体段的长度与速度配置有效"
                };
            },
            operationName: "CheckLineSegmentsValidity",
            defaultValue: new HealthCheckItem
            {
                Name = "LineSegmentsLengthAndSpeedValid",
                Status = HealthStatus.Unhealthy,
                Message = "检查线体段配置时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 检查上游连接配置是否有效
    /// </summary>
    private async Task<HealthCheckItem> CheckUpstreamConnectionConfigAsync(CancellationToken cancellationToken)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                await Task.Yield();
                var config = _communicationConfigRepository.Get();
                if (config == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "UpstreamConnectionConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = "上游通信配置未初始化"
                    };
                }

                // 检查根据通信模式，相应的连接地址是否已配置
                var isConfigured = config.Mode switch
                {
                    CommunicationMode.Tcp => !string.IsNullOrWhiteSpace(config.TcpServer),
                    CommunicationMode.SignalR => !string.IsNullOrWhiteSpace(config.SignalRHub),
                    CommunicationMode.Mqtt => !string.IsNullOrWhiteSpace(config.MqttBroker),
                    CommunicationMode.Http => !string.IsNullOrWhiteSpace(config.HttpApi),
                    _ => false
                };

                if (!isConfigured)
                {
                    return new HealthCheckItem
                    {
                        Name = "UpstreamConnectionConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"上游连接未配置：当前通信模式为 {config.Mode}，但对应的连接地址未设置"
                    };
                }

                // 检查实际连接状态
                var isConnected = _ruleEngineClient.IsConnected;
                if (!isConnected)
                {
                    return new HealthCheckItem
                    {
                        Name = "UpstreamConnectionConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"上游连接未建立：通信模式 {config.Mode}，配置已设置但尚未连接到 RuleEngine"
                    };
                }

                return new HealthCheckItem
                {
                    Name = "UpstreamConnectionConfigured",
                    Status = HealthStatus.Healthy,
                    Message = $"上游连接已建立：通信模式 {config.Mode}"
                };
            },
            operationName: "CheckUpstreamConnectionConfig",
            defaultValue: new HealthCheckItem
            {
                Name = "UpstreamConnectionConfigured",
                Status = HealthStatus.Unhealthy,
                Message = "检查上游连接配置时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 检查IO驱动器连接状态
    /// </summary>
    /// <remarks>
    /// 运行前检查是针对正式环境的检查，因此：
    /// - 仿真模式返回 Unhealthy（正式环境不应使用仿真驱动）
    /// - 未配置或配置不完整返回 Unhealthy
    /// </remarks>
    private async Task<HealthCheckItem> CheckIoDriverConnectivityAsync(CancellationToken cancellationToken)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                await Task.Yield();
                var ioConfig = _ioDriverConfigRepository.Get();
                
                if (ioConfig == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "IoDriverConnected",
                        Status = HealthStatus.Unhealthy,
                        Message = "IO驱动器配置未初始化"
                    };
                }

                // 获取厂商显示名称
                var vendorDisplayName = GetIoVendorDisplayName(ioConfig.VendorType);

                // 如果是仿真模式（不使用硬件驱动），返回不健康（正式环境运行准备检查）
                if (!ioConfig.UseHardwareDriver)
                {
                    return new HealthCheckItem
                    {
                        Name = "IoDriverConnected",
                        Status = HealthStatus.Unhealthy,
                        Message = $"IO驱动器处于仿真模式，厂商类型: {vendorDisplayName}（正式环境不应使用仿真驱动）"
                    };
                }

                // 验证硬件模式下的配置
                if (ioConfig.VendorType == DriverVendorType.Leadshine && ioConfig.Leadshine == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "IoDriverConnected",
                        Status = HealthStatus.Unhealthy,
                        Message = $"IO驱动器（{vendorDisplayName}）配置不完整，缺少雷赛控制卡参数"
                    };
                }

                // 硬件模式下，配置正确即视为就绪（实际连接状态需要驱动器运行时确认）
                return new HealthCheckItem
                {
                    Name = "IoDriverConnected",
                    Status = HealthStatus.Healthy,
                    Message = $"IO驱动器已配置，厂商: {vendorDisplayName}，硬件模式已启用"
                };
            },
            operationName: "CheckIoDriverConnectivity",
            defaultValue: new HealthCheckItem
            {
                Name = "IoDriverConnected",
                Status = HealthStatus.Unhealthy,
                Message = "检查IO驱动器连接状态时发生异常"
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 检查摆轮驱动器连接状态
    /// </summary>
    /// <remarks>
    /// 运行前检查是针对正式环境的检查，因此：
    /// - 仿真模式返回 Unhealthy（正式环境不应使用仿真驱动）
    /// - 未配置或未连接返回 Unhealthy
    /// </remarks>
    private async Task<HealthCheckItem> CheckWheelDiverterConnectivityAsync(CancellationToken cancellationToken)
    {
        return await _safeExecutor.ExecuteAsync(
            async () =>
            {
                await Task.Yield();
                var wheelConfig = _wheelDiverterConfigRepository.Get();
                
                if (wheelConfig == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "WheelDiverterDriverConnected",
                        Status = HealthStatus.Unhealthy,
                        Message = "摆轮驱动器配置未初始化"
                    };
                }

                // 获取厂商显示名称
                var vendorDisplayName = GetWheelDiverterVendorDisplayName(wheelConfig.VendorType);

                // 检查是否处于仿真模式
                var isSimulation = wheelConfig.VendorType switch
                {
                    WheelDiverterVendorType.ShuDiNiao => wheelConfig.ShuDiNiao?.UseSimulation ?? true,
                    WheelDiverterVendorType.Modi => wheelConfig.Modi?.UseSimulation ?? true,
                    _ => true
                };

                // 仿真模式返回不健康（正式环境运行准备检查）
                if (isSimulation)
                {
                    return new HealthCheckItem
                    {
                        Name = "WheelDiverterDriverConnected",
                        Status = HealthStatus.Unhealthy,
                        Message = $"摆轮驱动器处于仿真模式，厂商类型: {vendorDisplayName}（正式环境不应使用仿真驱动）"
                    };
                }

                // 检查实际连接状态（如果有驱动管理器）
                if (_wheelDiverterDriverManager != null)
                {
                    var activeDrivers = _wheelDiverterDriverManager.GetActiveDrivers();
                    var configuredDeviceCount = GetConfiguredWheelDiverterCount(wheelConfig);
                    var connectedCount = activeDrivers.Count;

                    if (connectedCount == 0 && configuredDeviceCount > 0)
                    {
                        return new HealthCheckItem
                        {
                            Name = "WheelDiverterDriverConnected",
                            Status = HealthStatus.Unhealthy,
                            Message = $"摆轮驱动器未连接：厂商 {vendorDisplayName}，已配置 {configuredDeviceCount} 台设备，但均未连接"
                        };
                    }

                    if (connectedCount < configuredDeviceCount)
                    {
                        return new HealthCheckItem
                        {
                            Name = "WheelDiverterDriverConnected",
                            Status = HealthStatus.Unhealthy,
                            Message = $"摆轮驱动器部分连接：厂商 {vendorDisplayName}，已配置 {configuredDeviceCount} 台设备，已连接 {connectedCount} 台"
                        };
                    }

                    return new HealthCheckItem
                    {
                        Name = "WheelDiverterDriverConnected",
                        Status = HealthStatus.Healthy,
                        Message = $"摆轮驱动器全部连接：厂商 {vendorDisplayName}，已连接 {connectedCount} 台设备"
                    };
                }

                // 没有驱动管理器时，只验证配置
                return new HealthCheckItem
                {
                    Name = "WheelDiverterDriverConnected",
                    Status = HealthStatus.Healthy,
                    Message = $"摆轮驱动器已配置，厂商: {vendorDisplayName}，硬件模式已启用"
                };
            },
            operationName: "CheckWheelDiverterConnectivity",
            defaultValue: new HealthCheckItem
            {
                Name = "WheelDiverterDriverConnected",
                Status = HealthStatus.Unhealthy,
                Message = "检查摆轮驱动器连接状态时发生异常"
            },
            cancellationToken: cancellationToken
        );
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

    /// <summary>
    /// 获取已配置的摆轮设备数量
    /// </summary>
    private static int GetConfiguredWheelDiverterCount(WheelDiverterConfiguration config)
    {
        return config.VendorType switch
        {
            WheelDiverterVendorType.ShuDiNiao => config.ShuDiNiao?.Devices.Count(d => d.IsEnabled) ?? 0,
            WheelDiverterVendorType.Modi => config.Modi?.Devices.Count(d => d.IsEnabled) ?? 0,
            _ => 0
        };
    }
}
