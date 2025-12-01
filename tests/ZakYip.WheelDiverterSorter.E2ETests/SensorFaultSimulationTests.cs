using ZakYip.WheelDiverterSorter.Core.Events.Chute;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Simulation.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 传感器故障和抖动仿真场景测试
/// </summary>
/// <remarks>
/// 验证传感器故障和抖动场景下，包裹能正确路由到异常口
/// </remarks>
public class SensorFaultSimulationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IUpstreamRoutingClient> _mockRuleEngineClient;
    private int _currentChuteId = 1;
    private readonly List<ParcelLifecycleContext> _lifecycleLogs = new();

    public SensorFaultSimulationTests()
    {
        // 创建模拟 RuleEngine 客户端
        _mockRuleEngineClient = new Mock<IUpstreamRoutingClient>(MockBehavior.Loose);
        _mockRuleEngineClient.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRuleEngineClient.Setup(x => x.DisconnectAsync())
            .Returns(Task.CompletedTask);
        _mockRuleEngineClient.Setup(x => x.IsConnected)
            .Returns(true);

        // 设置包裹检测通知的默认行为
        _mockRuleEngineClient
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long parcelId, CancellationToken ct) =>
            {
                var chuteId = GetNextChuteId();
                // 立即触发事件（同步），确保事件处理器已订阅
                // EventHandler<T> requires (sender, eventArgs)
                _mockRuleEngineClient.Raise(
                    x => x.ChuteAssignmentReceived += null,
                    _mockRuleEngineClient.Object,  // sender
                    new ChuteAssignmentNotificationEventArgs { ParcelId = parcelId, ChuteId = (int)chuteId , NotificationTime = DateTimeOffset.Now }
                );
                return true;
            });

        // 配置服务集合
        var services = new ServiceCollection();

        // 添加日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 添加包裹生命周期日志记录器（Mock版本用于测试）
        var mockLifecycleLogger = new Mock<IParcelLifecycleLogger>();
        mockLifecycleLogger
            .Setup(x => x.LogCreated(It.IsAny<ParcelLifecycleContext>()))
            .Callback<ParcelLifecycleContext>(ctx => _lifecycleLogs.Add(ctx));
        mockLifecycleLogger
            .Setup(x => x.LogSensorPassed(It.IsAny<ParcelLifecycleContext>(), It.IsAny<string>()))
            .Callback<ParcelLifecycleContext, string>((ctx, sensorName) => _lifecycleLogs.Add(ctx));
        mockLifecycleLogger
            .Setup(x => x.LogChuteAssigned(It.IsAny<ParcelLifecycleContext>(), It.IsAny<long>()))
            .Callback<ParcelLifecycleContext, long>((ctx, chuteId) => _lifecycleLogs.Add(ctx));
        mockLifecycleLogger
            .Setup(x => x.LogCompleted(It.IsAny<ParcelLifecycleContext>(), It.IsAny<ParcelFinalStatus>()))
            .Callback<ParcelLifecycleContext, ParcelFinalStatus>((ctx, status) => _lifecycleLogs.Add(ctx));
        mockLifecycleLogger
            .Setup(x => x.LogException(It.IsAny<ParcelLifecycleContext>(), It.IsAny<string>()))
            .Callback<ParcelLifecycleContext, string>((ctx, reason) => _lifecycleLogs.Add(ctx));
        services.AddSingleton(mockLifecycleLogger.Object);

        // 添加核心配置仓储（使用模拟）
        var mockRouteRepo = new Mock<IRouteConfigurationRepository>();
        mockRouteRepo.Setup(x => x.GetByChuteId(It.IsAny<int>()))
            .Returns((int chuteId) =>
            {
                if (chuteId >= 1 && chuteId <= 5)
                {
                    return new ChuteRouteConfiguration
                    {
                        ChuteId = chuteId,
                        ChuteName = $"Chute {chuteId}",
                        DiverterConfigurations = new List<DiverterConfigurationEntry>
                        {
                            new DiverterConfigurationEntry
                            {
                                DiverterId = 1,
                                TargetDirection = DiverterDirection.Straight,
                                SequenceNumber = 1
                            }
                        },
                        BeltSpeedMmPerSecond = 1000.0,
                        BeltLengthMm = 5000.0,
                        ToleranceTimeMs = 2000,
                        IsEnabled = true
                    };
                }
                return null;
            });
        services.AddSingleton(mockRouteRepo.Object);

        // 注册其他必要服务
        services.AddSingleton<ISwitchingPathGenerator, DefaultSwitchingPathGenerator>();
        services.AddSingleton(_mockRuleEngineClient.Object);
        services.AddSingleton<PrometheusMetrics>();
        // ParcelTimelineFactory is created per-scenario with specific options
        
        // Mock path executor - always succeeds
        services.AddSingleton<ISwitchingPathExecutor>(sp =>
        {
            var mockExecutor = new Mock<ISwitchingPathExecutor>();
            mockExecutor.Setup(x => x.ExecuteAsync(It.IsAny<SwitchingPath>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwitchingPath path, CancellationToken ct) =>
                {
                    return new PathExecutionResult
                    {
                        IsSuccess = true,
                        ActualChuteId = path.TargetChuteId
                    };
                });
            return mockExecutor.Object;
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    private long GetNextChuteId()
    {
        var chuteId = _currentChuteId;
        _currentChuteId = (_currentChuteId % 3) + 1; // 在 1, 2, 3 之间循环
        return chuteId;
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// 运行仿真场景
    /// </summary>
    private async Task<SimulationSummary> RunScenarioAsync(SimulationScenario scenario)
    {
        // 创建新的服务作用域，使用场景的配置
        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        // 创建带场景配置的 SimulationRunner
        var options = Options.Create(scenario.Options);
        var ruleEngineClient = services.GetRequiredService<IUpstreamRoutingClient>();
        var pathGenerator = services.GetRequiredService<ISwitchingPathGenerator>();
        var pathExecutor = _serviceProvider.GetRequiredService<ISwitchingPathExecutor>();
        
        // 为每个场景创建新的 ParcelTimelineFactory，使用场景专属配置
        var timelineFactory = new ParcelTimelineFactory(
            options,
            services.GetRequiredService<ILogger<ParcelTimelineFactory>>());
        
        var reportPrinter = new SimulationReportPrinter(services.GetRequiredService<ILogger<SimulationReportPrinter>>());
        var metrics = services.GetRequiredService<PrometheusMetrics>();
        var logger = services.GetRequiredService<ILogger<SimulationRunner>>();

        var lifecycleLogger = services.GetRequiredService<IParcelLifecycleLogger>();

        var runner = new SimulationRunner(
            options,
            ruleEngineClient,
            pathGenerator,
            pathExecutor,
            timelineFactory,
            reportPrinter,
            metrics,
            logger,
            lifecycleLogger);

        return await runner.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// 场景 SF-1：摆轮前传感器故障测试（100% 确定性故障）
    /// </summary>
    /// <remarks>
    /// 验证当摆轮前传感器持续不触发时，所有包裹被路由到异常口
    /// </remarks>
    [Fact]
    [SimulationScenario("ScenarioSF1_PreDiverterSensorFault_RouteToException")]
    public async Task ScenarioSF1_PreDiverterSensorFault_RouteToExceptionChute()
    {
        // Arrange - 使用999个包裹进行完整测试（根据需求）
        var scenario = ScenarioDefinitions.CreateScenarioSF1("FixedChute", parcelCount: 999);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert - 验证所有包裹都因传感器故障路由到异常口
        summary.Should().NotBeNull();
        summary.TotalParcels.Should().Be(999, "总包裹数应该是999");
        
        // 所有包裹应该是传感器故障状态
        var sensorFaultParcels = summary.Parcels
            .Where(r => r.Status == ParcelSimulationStatus.SensorFault)
            .ToList();
        
        sensorFaultParcels.Count.Should().Be(999, "所有包裹都应该是传感器故障状态");
        
        // 验证所有传感器故障包裹的失败原因和最终格口
        foreach (var parcel in sensorFaultParcels)
        {
            parcel.FailureReason.Should().Be("摆轮前传感器故障", $"包裹 {parcel.ParcelId} 的失败原因应该是摆轮前传感器故障");
            parcel.FinalChuteId.Should().Be(999, $"包裹 {parcel.ParcelId} 应该路由到异常格口999");
        }
        
        // 不应该有错分
        summary.SortedToWrongChuteCount.Should().Be(0, "不应该有包裹被错误分拣");
    }

    /// <summary>
    /// 场景 SJ-1：传感器抖动测试（高频抖动但不是所有包裹）
    /// </summary>
    /// <remarks>
    /// 验证当传感器短时间内多次触发时，重复的包裹被识别并路由到异常口
    /// 不要求所有包裹都异常，只要求抖动的包裹异常，正常的可以正常分拣
    /// </remarks>
    [Fact]
    [SimulationScenario("ScenarioSJ1_SensorJitter_DuplicatePackages")]
    public async Task ScenarioSJ1_SensorJitter_DuplicatePackagesRouteToException()
    {
        // Arrange - 使用30个包裹，约40%会抖动
        var scenario = ScenarioDefinitions.CreateScenarioSJ1("FixedChute", parcelCount: 30);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalParcels.Should().BeGreaterThanOrEqualTo(30, "总包裹数应该至少是30");
        
        // 验证至少存在一个传感器故障的包裹（抖动产生的）
        var sensorFaultParcels = summary.Parcels
            .Where(r => r.Status == ParcelSimulationStatus.SensorFault)
            .ToList();
        
        sensorFaultParcels.Should().NotBeEmpty("应该存在至少一个因传感器抖动而异常的包裹");
        
        // 验证所有传感器故障包裹的失败原因包含"抖动"相关信息
        foreach (var parcel in sensorFaultParcels)
        {
            parcel.FailureReason.Should().Contain("抖动", $"包裹 {parcel.ParcelId} 的失败原因应该包含'抖动'");
            parcel.FinalChuteId.Should().Be(999, $"包裹 {parcel.ParcelId} 应该路由到异常格口999");
        }
        
        // 验证正常包裹可以正常分拣（不要求所有包裹都异常）
        var normalParcels = summary.Parcels
            .Where(r => r.Status == ParcelSimulationStatus.SortedToTargetChute)
            .ToList();
        
        // 可以有正常包裹，也可以没有（取决于抖动概率）
        // 但如果有正常包裹，它们应该被正确分拣
        foreach (var parcel in normalParcels)
        {
            parcel.FinalChuteId.Should().Be(parcel.TargetChuteId, 
                $"正常包裹 {parcel.ParcelId} 应该被分拣到目标格口");
        }
        
        // 验证没有错分
        summary.SortedToWrongChuteCount.Should().Be(0, "不应该有包裹被错误分拣");
    }

    /// <summary>
    /// 测试包裹生命周期日志被正确记录
    /// </summary>
    [Fact]
    [SimulationScenario("SensorFault_LifecycleLogger_RecordsEvents")]
    public async Task LifecycleLogger_RecordsParcelEvents()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioA("FixedChute", parcelCount: 3);
        _lifecycleLogs.Clear();

        // Act
        await RunScenarioAsync(scenario);

        // Assert - 验证生命周期日志被记录
        _lifecycleLogs.Should().NotBeEmpty("应该记录包裹生命周期事件");
        
        // 验证每个包裹至少有一次日志记录
        var uniqueParcelIds = _lifecycleLogs.Select(l => l.ParcelId).Distinct().Count();
        uniqueParcelIds.Should().BeGreaterThan(0, "应该有包裹的生命周期被记录");
    }

    /// <summary>
    /// 集成测试：验证传感器故障场景下包裹生命周期日志完整性
    /// </summary>
    [Fact]
    [SimulationScenario("SensorFault_WithLifecycleLogging")]
    public async Task IntegrationTest_SensorFault_WithLifecycleLogging()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioSF1("FixedChute", parcelCount: 3);
        _lifecycleLogs.Clear();

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalParcels.Should().Be(3);
        
        // 验证没有错分
        summary.SortedToWrongChuteCount.Should().Be(0);
        
        // 验证生命周期日志
        _lifecycleLogs.Should().NotBeEmpty("应该记录传感器故障场景的包裹生命周期");
    }
}
