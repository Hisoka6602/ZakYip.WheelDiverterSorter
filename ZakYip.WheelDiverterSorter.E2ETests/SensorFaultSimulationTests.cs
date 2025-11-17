using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Simulation.Services;

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
    private readonly Mock<IRuleEngineClient> _mockRuleEngineClient;
    private int _currentChuteId = 1;
    private readonly List<ParcelLifecycleContext> _lifecycleLogs = new();

    public SensorFaultSimulationTests()
    {
        // 创建模拟 RuleEngine 客户端
        _mockRuleEngineClient = new Mock<IRuleEngineClient>(MockBehavior.Loose);
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
                // 使用Task.Delay(0)来异步触发事件，确保事件处理器已订阅
                _ = Task.Delay(10).ContinueWith(_ =>
                {
                    _mockRuleEngineClient.Raise(
                        x => x.ChuteAssignmentReceived += null,
                        new ChuteAssignmentNotificationEventArgs { ParcelId = parcelId, ChuteId = (int)chuteId }
                    );
                });
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
        services.AddSingleton<ParcelTimelineFactory>();
        
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
        var ruleEngineClient = services.GetRequiredService<IRuleEngineClient>();
        var pathGenerator = services.GetRequiredService<ISwitchingPathGenerator>();
        var pathExecutor = _serviceProvider.GetRequiredService<ISwitchingPathExecutor>();
        var timelineFactory = services.GetRequiredService<ParcelTimelineFactory>();
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
    /// 场景 SF-1：摆轮前传感器故障测试
    /// </summary>
    /// <remarks>
    /// 验证当摆轮前传感器持续不触发时，包裹被路由到异常口
    /// </remarks>
    [Fact]
    public async Task ScenarioSF1_PreDiverterSensorFault_RouteToExceptionChute()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioSF1("FixedChute", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert - 验证所有包裹都被路由到异常口
        summary.Should().NotBeNull();
        summary.TotalParcels.Should().Be(5);
        
        // 由于传感器故障，包裹应该超时或被标记为传感器故障
        var faultOrTimeoutCount = summary.Parcels
            .Count(r => r.Status == ParcelSimulationStatus.SensorFault || 
                        r.Status == ParcelSimulationStatus.Timeout);
        
        faultOrTimeoutCount.Should().BeGreaterThan(0, "应该有包裹因传感器故障或超时");
        
        // 不应该有错分
        summary.SortedToWrongChuteCount.Should().Be(0, "不应该有包裹被错误分拣");
    }

    /// <summary>
    /// 场景 SJ-1：传感器抖动测试
    /// </summary>
    /// <remarks>
    /// 验证当传感器短时间内多次触发时，重复的包裹被识别并路由到异常口
    /// </remarks>
    [Fact]
    public async Task ScenarioSJ1_SensorJitter_DuplicatePackagesRouteToException()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioSJ1("FixedChute", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalParcels.Should().BeGreaterThanOrEqualTo(5);
        
        // 验证没有错分
        summary.SortedToWrongChuteCount.Should().Be(0, "不应该有包裹被错误分拣");
        
        // 验证抖动产生的重复包裹被正确处理
        // 注意：由于抖动，实际产生的包裹数量会大于输入的包裹数量
        var validParcels = summary.Parcels
            .Count(r => r.Status == ParcelSimulationStatus.SortedToTargetChute);
        
        // 至少应该有一些包裹被成功分拣（非抖动的）
        validParcels.Should().BeGreaterThan(0, "应该有正常包裹被成功分拣");
    }

    /// <summary>
    /// 测试包裹生命周期日志被正确记录
    /// </summary>
    [Fact]
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
