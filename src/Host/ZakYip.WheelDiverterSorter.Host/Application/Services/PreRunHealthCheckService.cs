// 本文件使用向后兼容API，抑制废弃警告
#pragma warning disable CS0618 // Type or member is obsolete

using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

namespace ZakYip.WheelDiverterSorter.Host.Application.Services;

/// <summary>
/// 运行前健康检查服务实现
/// Pre-run health check service implementation
/// </summary>
public class PreRunHealthCheckService : IPreRunHealthCheckService
{
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly IPanelConfigurationRepository _panelConfigRepository;
    private readonly IChutePathTopologyRepository _topologyRepository;
    private readonly ICommunicationConfigurationRepository _communicationConfigRepository;
    private readonly IDriverConfigurationRepository _ioDriverConfigRepository;
    private readonly IWheelDiverterConfigurationRepository _wheelDiverterConfigRepository;
    private readonly IWheelDiverterDriverManager? _wheelDiverterDriverManager;
    private readonly IUpstreamRoutingClient _upstreamClient;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PreRunHealthCheckService> _logger;

    public PreRunHealthCheckService(
        ISystemConfigurationRepository systemConfigRepository,
        IPanelConfigurationRepository panelConfigRepository,
        IChutePathTopologyRepository topologyRepository,
        ICommunicationConfigurationRepository communicationConfigRepository,
        IDriverConfigurationRepository ioDriverConfigRepository,
        IWheelDiverterConfigurationRepository wheelDiverterConfigRepository,
        IUpstreamRoutingClient upstreamClient,
        ISafeExecutionService safeExecutor,
        ILogger<PreRunHealthCheckService> logger,
        IWheelDiverterDriverManager? wheelDiverterDriverManager = null)
    {
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _communicationConfigRepository = communicationConfigRepository ?? throw new ArgumentNullException(nameof(communicationConfigRepository));
        _ioDriverConfigRepository = ioDriverConfigRepository ?? throw new ArgumentNullException(nameof(ioDriverConfigRepository));
        _wheelDiverterConfigRepository = wheelDiverterConfigRepository ?? throw new ArgumentNullException(nameof(wheelDiverterConfigRepository));
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
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

        // 4. 上游连接配置检查
        checks.Add(await CheckUpstreamConnectionConfigAsync(cancellationToken));

        // 5. IO驱动器连接状态检查
        checks.Add(await CheckIoDriverConnectivityAsync(cancellationToken));

        // 6. 摆轮驱动器连接状态检查
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
                var topology = _topologyRepository.Get();
                if (topology == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = "格口路径拓扑未配置，无法验证异常口"
                    };
                }

                // 检查异常口ID是否与拓扑配置一致
                if (topology.ExceptionChuteId != exceptionChuteId)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"系统配置的异常口 {exceptionChuteId} 与拓扑配置的异常口 {topology.ExceptionChuteId} 不一致"
                    };
                }

                return new HealthCheckItem
                {
                    Name = "ExceptionChuteConfigured",
                    Status = HealthStatus.Healthy,
                    Message = $"异常口 {exceptionChuteId} 已配置且与拓扑配置一致"
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
                var topology = _topologyRepository.Get();
                if (topology == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "ChutePathTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "格口路径拓扑未配置"
                    };
                }

                // 检查是否有摆轮节点
                if (topology.DiverterNodes.Count == 0)
                {
                    return new HealthCheckItem
                    {
                        Name = "ChutePathTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "拓扑中未配置任何摆轮节点"
                    };
                }

                // 检查入口传感器ID
                if (topology.EntrySensorId <= 0)
                {
                    return new HealthCheckItem
                    {
                        Name = "ChutePathTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = "入口传感器ID未配置"
                    };
                }

                // 检查每个摆轮节点是否有格口配置
                var nodesWithoutChutes = topology.DiverterNodes
                    .Where(n => n.LeftChuteIds.Count == 0 && n.RightChuteIds.Count == 0)
                    .Select(n => n.DiverterId.ToString())
                    .ToList();

                if (nodesWithoutChutes.Any())
                {
                    return new HealthCheckItem
                    {
                        Name = "ChutePathTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = $"以下摆轮节点未配置任何格口：{string.Join("、", nodesWithoutChutes)}"
                    };
                }

                return new HealthCheckItem
                {
                    Name = "ChutePathTopologyValid",
                    Status = HealthStatus.Healthy,
                    Message = $"拓扑配置完整，共 {topology.DiverterNodes.Count} 个摆轮节点，{topology.TotalChuteCount} 个格口"
                };
            },
            operationName: "CheckTopologyCompleteness",
            defaultValue: new HealthCheckItem
            {
                Name = "ChutePathTopologyValid",
                Status = HealthStatus.Unhealthy,
                Message = "检查拓扑完整性时发生异常"
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
                var isConnected = _upstreamClient.IsConnected;
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
