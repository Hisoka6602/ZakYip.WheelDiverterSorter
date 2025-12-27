using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.SelfTest;

/// <summary>
/// 详细配置验证器
/// Detailed configuration validator that performs comprehensive checks
/// </summary>
/// <remarks>
/// 与 DefaultConfigValidator 相比，此验证器执行更详细的检查：
/// - 异常口配置及其与拓扑的一致性
/// - 面板IO配置完整性
/// - 摆轮拓扑完整性（节点数、格口配置、入口传感器）
/// - 上游连接配置
/// </remarks>
public class DetailedConfigValidator : IConfigValidator
{
    private readonly ISystemConfigService _systemConfigService;
    private readonly IPanelConfigurationRepository _panelConfigRepository;
    private readonly IChutePathTopologyRepository _topologyRepository;
    private readonly ICommunicationConfigurationRepository _communicationConfigRepository;
    private readonly ILogger<DetailedConfigValidator> _logger;
    private readonly ISystemClock _clock;

    public DetailedConfigValidator(
        ISystemConfigService systemConfigService,
        IPanelConfigurationRepository panelConfigRepository,
        IChutePathTopologyRepository topologyRepository,
        ICommunicationConfigurationRepository communicationConfigRepository,
        ILogger<DetailedConfigValidator> logger,
        ISystemClock clock)
    {
        _systemConfigService = systemConfigService ?? throw new ArgumentNullException(nameof(systemConfigService));
        _panelConfigRepository = panelConfigRepository ?? throw new ArgumentNullException(nameof(panelConfigRepository));
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
        _communicationConfigRepository = communicationConfigRepository ?? throw new ArgumentNullException(nameof(communicationConfigRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc/>
    public Task<ConfigHealthStatus> ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始详细配置验证...");

            var errors = new List<string>();

            // 1. 验证系统配置
            var systemConfig = _systemConfigService.GetSystemConfig();
            if (systemConfig == null)
            {
                errors.Add("系统配置未初始化");
            }
            else
            {
                // 验证异常格口
                if (systemConfig.ExceptionChuteId <= 0)
                {
                    errors.Add("异常口ID未配置或配置为无效值");
                }
                else
                {
                    // 检查异常口是否存在于拓扑中
                    var topologyForException = _topologyRepository.Get();
                    if (topologyForException == null)
                    {
                        errors.Add("格口路径拓扑未配置，无法验证异常口");
                    }
                    else if (topologyForException.ExceptionChuteId != systemConfig.ExceptionChuteId)
                    {
                        errors.Add($"系统配置的异常口 {systemConfig.ExceptionChuteId} 与拓扑配置的异常口 {topologyForException.ExceptionChuteId} 不一致");
                    }
                }
            }

            // 2. 验证面板IO配置
            var panelConfig = _panelConfigRepository.Get();
            if (panelConfig == null || !panelConfig.Enabled)
            {
                errors.Add("面板 IO 配置未启用或未配置");
            }
            else
            {
                var missingConfigs = new List<string>();

                // 检查按钮输入配置
                if (!panelConfig.StartButtonInputBit.HasValue)
                    missingConfigs.Add("开始按钮 IO");
                if (!panelConfig.StopButtonInputBit.HasValue)
                    missingConfigs.Add("停止按钮 IO");
                if (panelConfig.EmergencyStopButtons.Count == 0)
                    missingConfigs.Add("急停按钮 IO（至少需要一个急停按钮）");

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
                    errors.Add($"缺少面板 IO 配置：{string.Join("、", missingConfigs)}");
                }

                // 验证配置有效性
                (bool isValid, string? errorMessage) = panelConfig.Validate();
                if (!isValid)
                {
                    errors.Add($"面板配置验证失败：{errorMessage}");
                }
            }

            // 3. 验证拓扑完整性
            var topology = _topologyRepository.Get();
            if (topology == null)
            {
                errors.Add("格口路径拓扑未配置");
            }
            else
            {
                // 检查是否有摆轮节点
                if (topology.DiverterNodes.Count == 0)
                {
                    errors.Add("拓扑中未配置任何摆轮节点");
                }

                // 检查入口传感器ID
                if (topology.EntrySensorId <= 0)
                {
                    errors.Add("入口传感器ID未配置");
                }

                // 检查每个摆轮节点是否有格口配置
                var nodesWithoutChutes = topology.DiverterNodes
                    .Where(n => n.LeftChuteIds.Count == 0 && n.RightChuteIds.Count == 0)
                    .Select(n => n.DiverterId.ToString())
                    .ToList();

                if (nodesWithoutChutes.Any())
                {
                    errors.Add($"以下摆轮节点未配置任何格口：{string.Join("、", nodesWithoutChutes)}");
                }
            }

            // 4. 验证上游连接配置
            var commConfig = _communicationConfigRepository.Get();
            if (commConfig == null)
            {
                errors.Add("上游通信配置未初始化");
            }
            else
            {
                var isConfigured = commConfig.Mode switch
                {
                    Core.Enums.Communication.CommunicationMode.Tcp => !string.IsNullOrWhiteSpace(commConfig.TcpServer),
                    Core.Enums.Communication.CommunicationMode.SignalR => !string.IsNullOrWhiteSpace(commConfig.SignalRHub),
                    Core.Enums.Communication.CommunicationMode.Mqtt => !string.IsNullOrWhiteSpace(commConfig.MqttBroker),
                    _ => false
                };

                if (!isConfigured)
                {
                    errors.Add($"上游连接未配置：当前通信模式为 {commConfig.Mode}，但对应的连接地址未设置");
                }
            }

            if (errors.Any())
            {
                var errorMessage = string.Join("; ", errors);
                _logger.LogWarning("详细配置验证失败: {ErrorMessage}", errorMessage);
                return Task.FromResult(new ConfigHealthStatus
                {
                    IsValid = false,
                    ErrorMessage = errorMessage
                });
            }

            _logger.LogInformation("详细配置验证成功");
            return Task.FromResult(new ConfigHealthStatus
            {
                IsValid = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "详细配置验证失败");
            return Task.FromResult(new ConfigHealthStatus
            {
                IsValid = false,
                ErrorMessage = $"配置验证异常: {ex.Message}"
            });
        }
    }
}
