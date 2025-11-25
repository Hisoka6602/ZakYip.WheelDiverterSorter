// 本文件使用向后兼容API，抑制废弃警告
#pragma warning disable CS0618 // Type or member is obsolete

using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

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
    private readonly IRuleEngineClient _ruleEngineClient;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<PreRunHealthCheckService> _logger;

    public PreRunHealthCheckService(
        ISystemConfigurationRepository systemConfigRepository,
        IPanelConfigurationRepository panelConfigRepository,
        ILineTopologyRepository lineTopologyRepository,
        ICommunicationConfigurationRepository communicationConfigRepository,
        IRuleEngineClient ruleEngineClient,
        ISafeExecutionService safeExecutor,
        ILogger<PreRunHealthCheckService> logger)
    {
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _lineTopologyRepository = lineTopologyRepository ?? throw new ArgumentNullException(nameof(lineTopologyRepository));
        _communicationConfigRepository = communicationConfigRepository ?? throw new ArgumentNullException(nameof(communicationConfigRepository));
        _ruleEngineClient = ruleEngineClient ?? throw new ArgumentNullException(nameof(ruleEngineClient));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                // 检查异常口是否有有效路径
                var path = topology.GetPathToChute(exceptionChuteId.ToString());
                if (path == null || path.Count == 0)
                {
                    return new HealthCheckItem
                    {
                        Name = "ExceptionChuteConfigured",
                        Status = HealthStatus.Unhealthy,
                        Message = $"异常口 {exceptionChuteId} 无可达路径"
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

                var entryToFirstWheelSegment = topology.FindSegment(LineTopologyConfig.EntryNodeId, firstWheel.NodeId);
                if (entryToFirstWheelSegment == null)
                {
                    return new HealthCheckItem
                    {
                        Name = "LineTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = $"找不到从入口到首个摆轮 {firstWheel.NodeId} 的路径"
                    };
                }

                // 检查每个配置的格口是否能通过拓扑反查到路径
                var unreachableChutes = new List<string>();
                foreach (var chute in topology.Chutes)
                {
                    var path = topology.GetPathToChute(chute.ChuteId);
                    if (path == null || path.Count == 0)
                    {
                        unreachableChutes.Add(chute.ChuteId);
                    }
                }

                if (unreachableChutes.Any())
                {
                    return new HealthCheckItem
                    {
                        Name = "LineTopologyValid",
                        Status = HealthStatus.Unhealthy,
                        Message = $"以下格口无可达路径：{string.Join("、", unreachableChutes)}"
                    };
                }

                return new HealthCheckItem
                {
                    Name = "LineTopologyValid",
                    Status = HealthStatus.Healthy,
                    Message = $"拓扑配置完整，共 {topology.WheelNodes.Count} 个摆轮节点，{topology.Chutes.Count} 个格口，所有路径可达"
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
                    else if (segment.NominalSpeedMmPerSec <= 0)
                    {
                        invalidSegments.Add($"{segment.SegmentId}(速度={segment.NominalSpeedMmPerSec}mm/s)");
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

                // 特别检查入口到首个摆轮的路径上的线体段
                var firstWheel = topology.WheelNodes.OrderBy(n => n.PositionIndex).FirstOrDefault();
                if (firstWheel != null)
                {
                    var criticalSegment = topology.FindSegment(LineTopologyConfig.EntryNodeId, firstWheel.NodeId);
                    if (criticalSegment != null)
                    {
                        if (criticalSegment.LengthMm <= 0 || criticalSegment.NominalSpeedMmPerSec <= 0)
                        {
                            return new HealthCheckItem
                            {
                                Name = "LineSegmentsLengthAndSpeedValid",
                                Status = HealthStatus.Unhealthy,
                                Message = $"关键路径段 {criticalSegment.SegmentId} 配置无效（长度={criticalSegment.LengthMm}mm，速度={criticalSegment.NominalSpeedMmPerSec}mm/s）"
                            };
                        }
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
}
