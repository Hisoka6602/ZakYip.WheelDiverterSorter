using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Application.Services;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 运行前健康检查服务单元测试
/// </summary>
public class PreRunHealthCheckServiceTests
{
    private readonly Mock<ISystemConfigurationRepository> _mockSystemConfigRepo;
    private readonly Mock<IPanelConfigurationRepository> _mockPanelConfigRepo;
    private readonly Mock<ILineTopologyRepository> _mockTopologyRepo;
    private readonly Mock<ICommunicationConfigurationRepository> _mockCommunicationConfigRepo;
    private readonly Mock<IDriverConfigurationRepository> _mockIoDriverConfigRepo;
    private readonly Mock<IWheelDiverterConfigurationRepository> _mockWheelDiverterConfigRepo;
    private readonly Mock<IRuleEngineClient> _mockRuleEngineClient;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly Mock<ILogger<PreRunHealthCheckService>> _mockLogger;
    private readonly PreRunHealthCheckService _service;

    public PreRunHealthCheckServiceTests()
    {
        _mockSystemConfigRepo = new Mock<ISystemConfigurationRepository>();
        _mockPanelConfigRepo = new Mock<IPanelConfigurationRepository>();
        _mockTopologyRepo = new Mock<ILineTopologyRepository>();
        _mockCommunicationConfigRepo = new Mock<ICommunicationConfigurationRepository>();
        _mockIoDriverConfigRepo = new Mock<IDriverConfigurationRepository>();
        _mockWheelDiverterConfigRepo = new Mock<IWheelDiverterConfigurationRepository>();
        _mockRuleEngineClient = new Mock<IRuleEngineClient>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        _mockLogger = new Mock<ILogger<PreRunHealthCheckService>>();

        // 配置 SafeExecutionService Mock 默认行为：直接执行传入的函数
        _mockSafeExecutor
            .Setup(s => s.ExecuteAsync(
                It.IsAny<Func<Task<HealthCheckItem>>>(),
                It.IsAny<string>(),
                It.IsAny<HealthCheckItem>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<HealthCheckItem>>, string, HealthCheckItem, CancellationToken>(
                (func, _, _, _) => func());

        // 设置默认的通信配置（有效配置）
        _mockCommunicationConfigRepo.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "192.168.1.100:8000"
        });

        // 设置默认的 RuleEngineClient 连接状态为已连接
        _mockRuleEngineClient.Setup(c => c.IsConnected).Returns(true);

        // 设置默认的 IO 驱动器配置
        _mockIoDriverConfigRepo.Setup(r => r.Get()).Returns(DriverConfiguration.GetDefault());

        // 设置默认的摆轮驱动器配置
        _mockWheelDiverterConfigRepo.Setup(r => r.Get()).Returns(WheelDiverterConfiguration.GetDefault());

        _service = new PreRunHealthCheckService(
            _mockSystemConfigRepo.Object,
            _mockPanelConfigRepo.Object,
            _mockTopologyRepo.Object,
            _mockCommunicationConfigRepo.Object,
            _mockIoDriverConfigRepo.Object,
            _mockWheelDiverterConfigRepo.Object,
            _mockRuleEngineClient.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object
        );
    }

    #region 异常口配置检查测试

    [Fact]
    public async Task CheckExceptionChute_WhenSystemConfigIsNull_ShouldReturnUnhealthy()
    {
        // Arrange
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns((SystemConfiguration)null!);

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var exceptionChuteCheck = result.Checks.FirstOrDefault(c => c.Name == "ExceptionChuteConfigured");
        Assert.NotNull(exceptionChuteCheck);
        Assert.Equal(HealthStatus.Unhealthy, exceptionChuteCheck.Status);
        Assert.Contains("系统配置未初始化", exceptionChuteCheck.Message);
    }

    [Fact]
    public async Task CheckExceptionChute_WhenExceptionChuteIdIsInvalid_ShouldReturnUnhealthy()
    {
        // Arrange
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 0 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var exceptionChuteCheck = result.Checks.FirstOrDefault(c => c.Name == "ExceptionChuteConfigured");
        Assert.NotNull(exceptionChuteCheck);
        Assert.Equal(HealthStatus.Unhealthy, exceptionChuteCheck.Status);
        Assert.Contains("异常口ID未配置或配置为无效值", exceptionChuteCheck.Message);
    }

    [Fact]
    public async Task CheckExceptionChute_WhenExceptionChuteNotInTopology_ShouldReturnUnhealthy()
    {
        // Arrange
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);

        var topology = CreateMinimalValidTopology();
        // 不包含异常口
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var exceptionChuteCheck = result.Checks.FirstOrDefault(c => c.Name == "ExceptionChuteConfigured");
        Assert.NotNull(exceptionChuteCheck);
        Assert.Equal(HealthStatus.Unhealthy, exceptionChuteCheck.Status);
        Assert.Contains("不存在于线体拓扑中", exceptionChuteCheck.Message);
    }

    [Fact]
    public async Task CheckExceptionChute_WhenExceptionChuteValid_ShouldReturnHealthy()
    {
        // Arrange
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);

        var topology = CreateValidTopologyWithExceptionChute(999);
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        // Setup panel config and other checks to pass
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        var exceptionChuteCheck = result.Checks.FirstOrDefault(c => c.Name == "ExceptionChuteConfigured");
        Assert.NotNull(exceptionChuteCheck);
        Assert.Equal(HealthStatus.Healthy, exceptionChuteCheck.Status);
        Assert.Contains("已配置且存在于拓扑中", exceptionChuteCheck.Message);
    }

    #endregion

    #region 面板 IO 配置检查测试

    [Fact]
    public async Task CheckPanelIo_WhenPanelDisabled_ShouldReturnHealthy()
    {
        // Arrange
        var panelConfig = PanelConfiguration.GetDefault() with { Enabled = false };
        _mockPanelConfigRepo.Setup(r => r.Get()).Returns(panelConfig);

        SetupValidSystemConfig();
        SetupValidTopology();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        var panelCheck = result.Checks.FirstOrDefault(c => c.Name == "PanelIoConfigured");
        Assert.NotNull(panelCheck);
        Assert.Equal(HealthStatus.Healthy, panelCheck.Status);
        Assert.Contains("面板功能未启用", panelCheck.Message);
    }

    [Fact]
    public async Task CheckPanelIo_WhenMissingIoConfigurations_ShouldReturnUnhealthy()
    {
        // Arrange
        var panelConfig = PanelConfiguration.GetDefault() with
        {
            Enabled = true,
            StartButtonInputBit = null,  // 缺少开始按钮
            StopButtonInputBit = 1,
            EmergencyStopButtonInputBit = 2
        };
        _mockPanelConfigRepo.Setup(r => r.Get()).Returns(panelConfig);

        SetupValidSystemConfig();
        SetupValidTopology();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var panelCheck = result.Checks.FirstOrDefault(c => c.Name == "PanelIoConfigured");
        Assert.NotNull(panelCheck);
        Assert.Equal(HealthStatus.Unhealthy, panelCheck.Status);
        Assert.Contains("缺少面板 IO 配置", panelCheck.Message);
        Assert.Contains("开始按钮 IO", panelCheck.Message);
    }

    [Fact]
    public async Task CheckPanelIo_WhenAllIoConfigured_ShouldReturnHealthy()
    {
        // Arrange
        SetupValidPanelConfig();
        SetupValidSystemConfig();
        SetupValidTopology();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        var panelCheck = result.Checks.FirstOrDefault(c => c.Name == "PanelIoConfigured");
        Assert.NotNull(panelCheck);
        Assert.Equal(HealthStatus.Healthy, panelCheck.Status);
        Assert.Contains("面板 IO 配置完整且有效", panelCheck.Message);
    }

    #endregion

    #region 拓扑完整性检查测试

    [Fact]
    public async Task CheckTopology_WhenTopologyIsNull_ShouldReturnUnhealthy()
    {
        // Arrange
        _mockTopologyRepo.Setup(r => r.Get()).Returns((LineTopologyConfig)null!);

        SetupValidSystemConfig();
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var topologyCheck = result.Checks.FirstOrDefault(c => c.Name == "LineTopologyValid");
        Assert.NotNull(topologyCheck);
        Assert.Equal(HealthStatus.Unhealthy, topologyCheck.Status);
        Assert.Contains("线体拓扑未配置", topologyCheck.Message);
    }

    [Fact]
    public async Task CheckTopology_WhenNoWheelNodes_ShouldReturnUnhealthy()
    {
        // Arrange
        var topology = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test",
            WheelNodes = Array.Empty<WheelNodeConfig>(),
            Chutes = Array.Empty<ChuteConfig>(),
            LineSegments = Array.Empty<LineSegmentConfig>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        SetupValidSystemConfig();
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var topologyCheck = result.Checks.FirstOrDefault(c => c.Name == "LineTopologyValid");
        Assert.NotNull(topologyCheck);
        Assert.Equal(HealthStatus.Unhealthy, topologyCheck.Status);
        Assert.Contains("未配置任何摆轮节点", topologyCheck.Message);
    }

    [Fact]
    public async Task CheckTopology_WhenMissingEntryToFirstWheelSegment_ShouldReturnUnhealthy()
    {
        // Arrange
        var topology = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test",
            WheelNodes = new[]
            {
                new WheelNodeConfig
                {
                    NodeId = "WHEEL-1",
                    PositionIndex = 0,
                    NodeName = "First Wheel",
                }
            },
            Chutes = Array.Empty<ChuteConfig>(),
            LineSegments = Array.Empty<LineSegmentConfig>(),  // 缺少线体段
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        SetupValidSystemConfig();
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var topologyCheck = result.Checks.FirstOrDefault(c => c.Name == "LineTopologyValid");
        Assert.NotNull(topologyCheck);
        Assert.Equal(HealthStatus.Unhealthy, topologyCheck.Status);
        // 当线体段配置为空时，返回"线体段配置为空"的消息
        Assert.Contains("线体段配置为空", topologyCheck.Message);
    }

    [Fact]
    public async Task CheckTopology_WhenValid_ShouldReturnHealthy()
    {
        // Arrange
        SetupValidTopology();
        SetupValidSystemConfig();
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        var topologyCheck = result.Checks.FirstOrDefault(c => c.Name == "LineTopologyValid");
        Assert.NotNull(topologyCheck);
        Assert.Equal(HealthStatus.Healthy, topologyCheck.Status);
        Assert.Contains("拓扑配置完整", topologyCheck.Message);
    }

    #endregion

    #region 线体段配置检查测试

    [Fact]
    public async Task CheckLineSegments_WhenInvalidLength_ShouldReturnUnhealthy()
    {
        // Arrange
        var topology = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test",
            WheelNodes = new[]
            {
                new WheelNodeConfig
                {
                    NodeId = "WHEEL-1",
                    PositionIndex = 0,
                    NodeName = "First Wheel",
                }
            },
            Chutes = new[]
            {
                new ChuteConfig
                {
                    ChuteId = "100",
                    ChuteName = "Chute 100",
                    BoundNodeId = "WHEEL-1",
                    BoundDirection = "Left"
                }
            },
            LineSegments = new[]
            {
                new LineSegmentConfig
                {
                    SegmentId = 1,
                    StartIoId = 1, // Entry IO
                    EndIoId = 2, // Wheel-1 IO
                    LengthMm = -100,  // 无效长度
                    SpeedMmPerSec = 500
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        SetupValidSystemConfig();
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var segmentCheck = result.Checks.FirstOrDefault(c => c.Name == "LineSegmentsLengthAndSpeedValid");
        Assert.NotNull(segmentCheck);
        Assert.Equal(HealthStatus.Unhealthy, segmentCheck.Status);
        Assert.Contains("非法线体段配置", segmentCheck.Message);
    }

    [Fact]
    public async Task CheckLineSegments_WhenInvalidSpeed_ShouldReturnUnhealthy()
    {
        // Arrange
        var topology = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test",
            WheelNodes = new[]
            {
                new WheelNodeConfig
                {
                    NodeId = "WHEEL-1",
                    PositionIndex = 0,
                    NodeName = "First Wheel",
                }
            },
            Chutes = new[]
            {
                new ChuteConfig
                {
                    ChuteId = "100",
                    ChuteName = "Chute 100",
                    BoundNodeId = "WHEEL-1",
                    BoundDirection = "Left"
                }
            },
            LineSegments = new[]
            {
                new LineSegmentConfig
                {
                    SegmentId = 1,
                    StartIoId = 1, // Entry IO
                    EndIoId = 2, // Wheel-1 IO
                    LengthMm = 1000,
                    SpeedMmPerSec = 0  // 无效速度
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        SetupValidSystemConfig();
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var segmentCheck = result.Checks.FirstOrDefault(c => c.Name == "LineSegmentsLengthAndSpeedValid");
        Assert.NotNull(segmentCheck);
        Assert.Equal(HealthStatus.Unhealthy, segmentCheck.Status);
        Assert.Contains("非法线体段配置", segmentCheck.Message);
    }

    [Fact]
    public async Task CheckLineSegments_WhenValid_ShouldReturnHealthy()
    {
        // Arrange
        SetupValidTopology();
        SetupValidSystemConfig();
        SetupValidPanelConfig();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        var segmentCheck = result.Checks.FirstOrDefault(c => c.Name == "LineSegmentsLengthAndSpeedValid");
        Assert.NotNull(segmentCheck);
        Assert.Equal(HealthStatus.Healthy, segmentCheck.Status);
        Assert.Contains("所有", segmentCheck.Message);
        Assert.Contains("线体段的长度与速度配置有效", segmentCheck.Message);
    }

    #endregion

    #region 综合检查测试

    [Fact]
    public async Task ExecuteAsync_WhenAllChecksPass_ShouldReturnHealthy()
    {
        // Arrange
        SetupValidSystemConfig();
        SetupValidPanelConfig();
        SetupValidTopology();

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.OverallStatus);
        Assert.True(result.IsHealthy);
        Assert.All(result.Checks, check => Assert.Equal(HealthStatus.Healthy, check.Status));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnyCheckFails_ShouldReturnUnhealthy()
    {
        // Arrange
        SetupValidSystemConfig();
        SetupValidPanelConfig();
        _mockTopologyRepo.Setup(r => r.Get()).Returns((LineTopologyConfig)null!);  // 拓扑检查失败

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        Assert.False(result.IsHealthy);
        Assert.Contains(result.Checks, check => check.Status == HealthStatus.Unhealthy);
    }

    #endregion

    #region Helper Methods

    private void SetupValidSystemConfig()
    {
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);
    }

    private void SetupValidPanelConfig()
    {
        var panelConfig = PanelConfiguration.GetDefault() with
        {
            Enabled = true,
            StartButtonInputBit = 0,
            StopButtonInputBit = 1,
            EmergencyStopButtonInputBit = 2,
            StartLightOutputBit = 10,
            StopLightOutputBit = 11,
            ConnectionLightOutputBit = 12,
            SignalTowerRedOutputBit = 20,
            SignalTowerYellowOutputBit = 21,
            SignalTowerGreenOutputBit = 22
        };
        _mockPanelConfigRepo.Setup(r => r.Get()).Returns(panelConfig);
    }

    private void SetupValidTopology()
    {
        var topology = CreateValidTopologyWithExceptionChute(999);
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);
    }

    private LineTopologyConfig CreateMinimalValidTopology()
    {
        return new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test Topology",
            WheelNodes = new[]
            {
                new WheelNodeConfig
                {
                    NodeId = "WHEEL-1",
                    PositionIndex = 0,
                    NodeName = "First Wheel",
                }
            },
            Chutes = new[]
            {
                new ChuteConfig
                {
                    ChuteId = "100",
                    ChuteName = "Chute 100",
                    BoundNodeId = "WHEEL-1",
                    BoundDirection = "Left"
                }
            },
            LineSegments = new[]
            {
                new LineSegmentConfig
                {
                    SegmentId = 1,
                    StartIoId = 1, // Entry IO
                    EndIoId = 2, // Wheel-1 IO
                    LengthMm = 1000,
                    SpeedMmPerSec = 500
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private LineTopologyConfig CreateValidTopologyWithExceptionChute(long exceptionChuteId)
    {
        return new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test Topology",
            WheelNodes = new[]
            {
                new WheelNodeConfig
                {
                    NodeId = "WHEEL-1",
                    PositionIndex = 0,
                    NodeName = "First Wheel",
                }
            },
            Chutes = new[]
            {
                new ChuteConfig
                {
                    ChuteId = "100",
                    ChuteName = "Chute 100",
                    BoundNodeId = "WHEEL-1",
                    BoundDirection = "Left"
                },
                new ChuteConfig
                {
                    ChuteId = exceptionChuteId.ToString(),
                    ChuteName = "Exception Chute",
                    BoundNodeId = "WHEEL-1",
                    BoundDirection = "Right"
                }
            },
            LineSegments = new[]
            {
                new LineSegmentConfig
                {
                    SegmentId = 1,
                    StartIoId = 1, // Entry IO
                    EndIoId = 2, // Wheel-1 IO
                    LengthMm = 1000,
                    SpeedMmPerSec = 500
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region 线体段配置检查测试 - 新增：线体段为0检查

    [Fact]
    public async Task CheckLineSegments_WhenLineSegmentsCountIsZero_ShouldReturnUnhealthy()
    {
        // Arrange
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);

        var panelConfig = PanelConfiguration.GetDefault() with { Enabled = false };
        _mockPanelConfigRepo.Setup(r => r.Get()).Returns(panelConfig);

        // 创建一个没有线体段的拓扑
        var topology = new LineTopologyConfig
        {
            TopologyId = "TEST-TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = new[]
            {
                new WheelNodeConfig
                {
                    NodeId = "WHEEL-1",
                    PositionIndex = 0,
                    NodeName = "First Wheel",
                }
            },
            Chutes = new[]
            {
                new ChuteConfig
                {
                    ChuteId = "999",
                    ChuteName = "Exception Chute",
                    BoundNodeId = "WHEEL-1",
                    BoundDirection = "Right"
                }
            },
            LineSegments = Array.Empty<LineSegmentConfig>(), // 没有线体段
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var lineSegmentsCheck = result.Checks.FirstOrDefault(c => c.Name == "LineSegmentsLengthAndSpeedValid");
        Assert.NotNull(lineSegmentsCheck);
        Assert.Equal(HealthStatus.Unhealthy, lineSegmentsCheck.Status);
        Assert.Contains("线体段数量为0", lineSegmentsCheck.Message);
    }

    #endregion

    #region 上游连接配置检查测试 - 新增

    [Fact]
    public async Task CheckUpstreamConnection_WhenConfigIsNull_ShouldReturnUnhealthy()
    {
        // Arrange
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);

        var panelConfig = PanelConfiguration.GetDefault() with { Enabled = false };
        _mockPanelConfigRepo.Setup(r => r.Get()).Returns(panelConfig);

        var topology = CreateMinimalValidTopology();
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        _mockCommunicationConfigRepo.Setup(r => r.Get()).Returns((CommunicationConfiguration)null!);

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var upstreamCheck = result.Checks.FirstOrDefault(c => c.Name == "UpstreamConnectionConfigured");
        Assert.NotNull(upstreamCheck);
        Assert.Equal(HealthStatus.Unhealthy, upstreamCheck.Status);
        Assert.Contains("上游通信配置未初始化", upstreamCheck.Message);
    }

    [Fact]
    public async Task CheckUpstreamConnection_WhenTcpModeButNoServer_ShouldReturnUnhealthy()
    {
        // Arrange
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);

        var panelConfig = PanelConfiguration.GetDefault() with { Enabled = false };
        _mockPanelConfigRepo.Setup(r => r.Get()).Returns(panelConfig);

        var topology = CreateMinimalValidTopology();
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        // TCP模式但未配置服务器地址
        _mockCommunicationConfigRepo.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = null // 未配置
        });

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var upstreamCheck = result.Checks.FirstOrDefault(c => c.Name == "UpstreamConnectionConfigured");
        Assert.NotNull(upstreamCheck);
        Assert.Equal(HealthStatus.Unhealthy, upstreamCheck.Status);
        Assert.Contains("上游连接未配置", upstreamCheck.Message);
        Assert.Contains("Tcp", upstreamCheck.Message);
    }

    [Fact]
    public async Task CheckUpstreamConnection_WhenMqttModeButNoBroker_ShouldReturnUnhealthy()
    {
        // Arrange
        var systemConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        _mockSystemConfigRepo.Setup(r => r.Get()).Returns(systemConfig);

        var panelConfig = PanelConfiguration.GetDefault() with { Enabled = false };
        _mockPanelConfigRepo.Setup(r => r.Get()).Returns(panelConfig);

        var topology = CreateMinimalValidTopology();
        _mockTopologyRepo.Setup(r => r.Get()).Returns(topology);

        // MQTT模式但未配置Broker地址
        _mockCommunicationConfigRepo.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Mqtt,
            MqttBroker = "" // 未配置
        });

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var upstreamCheck = result.Checks.FirstOrDefault(c => c.Name == "UpstreamConnectionConfigured");
        Assert.NotNull(upstreamCheck);
        Assert.Equal(HealthStatus.Unhealthy, upstreamCheck.Status);
        Assert.Contains("上游连接未配置", upstreamCheck.Message);
        Assert.Contains("Mqtt", upstreamCheck.Message);
    }

    [Fact]
    public async Task CheckUpstreamConnection_WhenProperlyConfigured_ShouldReturnHealthy()
    {
        // Arrange
        SetupValidSystemConfig();
        SetupValidPanelConfig();
        SetupValidTopology();

        // 正确配置
        _mockCommunicationConfigRepo.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "192.168.1.100:8000"
        });

        // 设置连接状态为已连接
        _mockRuleEngineClient.Setup(c => c.IsConnected).Returns(true);

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.OverallStatus);
        var upstreamCheck = result.Checks.FirstOrDefault(c => c.Name == "UpstreamConnectionConfigured");
        Assert.NotNull(upstreamCheck);
        Assert.Equal(HealthStatus.Healthy, upstreamCheck.Status);
        Assert.Contains("上游连接已建立", upstreamCheck.Message);
    }

    [Fact]
    public async Task CheckUpstreamConnection_WhenNotConnected_ShouldReturnUnhealthy()
    {
        // Arrange
        SetupValidSystemConfig();
        SetupValidPanelConfig();
        SetupValidTopology();

        // 正确配置
        _mockCommunicationConfigRepo.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "192.168.1.100:8000"
        });

        // 设置连接状态为未连接
        _mockRuleEngineClient.Setup(c => c.IsConnected).Returns(false);

        // Act
        var result = await _service.ExecuteAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.OverallStatus);
        var upstreamCheck = result.Checks.FirstOrDefault(c => c.Name == "UpstreamConnectionConfigured");
        Assert.NotNull(upstreamCheck);
        Assert.Equal(HealthStatus.Unhealthy, upstreamCheck.Status);
        Assert.Contains("上游连接未建立", upstreamCheck.Message);
    }

    #endregion
}
