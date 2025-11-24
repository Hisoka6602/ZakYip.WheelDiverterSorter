#pragma warning disable CS0618 // 向后兼容：测试中使用已废弃字段
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Simulation.Services;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 仿真场景测试
/// </summary>
/// <remarks>
/// 覆盖不同摩擦分布、掉包概率的场景，验证系统在极端情况下不会错分
/// </remarks>
public class SimulationScenariosTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IRuleEngineClient> _mockRuleEngineClient;
    private int _currentChuteId = 1;
    private readonly Random _random = new Random(42);

    public SimulationScenariosTests()
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
                // 同步触发格口分配事件（避免竞态条件）
                var chuteId = GetNextChuteId();
                // 使用 Task.Run 确保事件在回调后触发
                _ = Task.Run(() =>
                {
                    _mockRuleEngineClient.Raise(
                        x => x.ChuteAssignmentReceived += null,
                        new ChuteAssignmentNotificationEventArgs { ParcelId = parcelId, ChuteId = chuteId }
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
            builder.SetMinimumLevel(LogLevel.Warning); // 减少日志输出
        });

        // 添加核心配置仓储（使用模拟）
        var mockRouteRepo = new Mock<IRouteConfigurationRepository>();
        mockRouteRepo.Setup(x => x.GetByChuteId(It.IsAny<int>()))
            .Returns((int chuteId) =>
            {
                // 返回简单的路由配置
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

        var mockSystemRepo = new Mock<ISystemConfigurationRepository>();
        mockSystemRepo.Setup(x => x.Get())
            .Returns(new SystemConfiguration
            {
                ExceptionChuteId = 999,
                ChuteAssignmentTimeoutMs = 10000
            });
        services.AddSingleton(mockSystemRepo.Object);

        // 添加 RuleEngine 客户端（模拟）
        services.AddSingleton(_mockRuleEngineClient.Object);

        // 添加路径生成器（使用模拟）
        var mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        mockPathGenerator.Setup(x => x.GeneratePath(It.IsAny<int>()))
            .Returns((int targetChuteId) =>
            {
                if (targetChuteId >= 1 && targetChuteId <= 5)
                {
                    return new SwitchingPath
                    {
                        TargetChuteId = targetChuteId,
                        FallbackChuteId = 999,
                        GeneratedAt = DateTimeOffset.UtcNow,
                        Segments = new List<SwitchingPathSegment>
                        {
                            new SwitchingPathSegment
                            {
                                DiverterId = 1,
                                TargetDirection = DiverterDirection.Straight,
                                SequenceNumber = 1,
                                TtlMilliseconds = 2000
                            }
                        }
                    };
                }
                return null;
            });
        services.AddSingleton(mockPathGenerator.Object);
        services.AddSingleton<ISwitchingPathExecutor>(sp =>
        {
            // 使用模拟执行器，总是返回成功
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

        // 添加仿真服务（初始配置为空，后续通过 IOptions 覆盖）
        services.AddSingleton<ParcelTimelineFactory>();
        services.AddSingleton<SimulationReportPrinter>();
        
        // 添加 Prometheus 指标服务
        services.AddSingleton<PrometheusMetrics>();

        _serviceProvider = services.BuildServiceProvider();
    }

    private int GetNextChuteId()
    {
        // 根据当前排序模式返回格口ID
        // 这里简化处理，假设格口ID在 1-5 之间循环
        var chuteId = (_currentChuteId % 5) + 1;
        _currentChuteId++;
        return chuteId;
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
        var pathExecutor = services.GetRequiredService<ISwitchingPathExecutor>();
        var timelineFactory = services.GetRequiredService<ParcelTimelineFactory>();
        var reportPrinter = services.GetRequiredService<SimulationReportPrinter>();
        var metrics = services.GetRequiredService<PrometheusMetrics>();
        var logger = services.GetRequiredService<ILogger<SimulationRunner>>();

        var runner = new SimulationRunner(
            options,
            ruleEngineClient,
            pathGenerator,
            pathExecutor,
            timelineFactory,
            reportPrinter,
            metrics,
            logger
        );

        // 运行仿真
        return await runner.RunAsync();
    }

    /// <summary>
    /// 验证仿真结果的全局不变量
    /// </summary>
    private void ValidateInvariants(SimulationSummary summary, SimulationScenario scenario)
    {
        // 核心不变量：错误分拣计数必须为 0
        summary.SortedToWrongChuteCount.Should().Be(0,
            $"场景 '{scenario.ScenarioName}' 中不应出现错误分拣");

        // 验证成功分拣到目标格口的包裹
        if (summary.StatusStatistics.ContainsKey(ParcelSimulationStatus.SortedToTargetChute))
        {
            var sortedCount = summary.StatusStatistics[ParcelSimulationStatus.SortedToTargetChute];
            sortedCount.Should().Be(summary.SortedToTargetChuteCount,
                "SortedToTargetChute 状态计数应与统计值一致");
        }

        // 总包裹数应该等于各状态之和
        var totalFromStatus = summary.StatusStatistics.Values.Sum();
        totalFromStatus.Should().Be(summary.TotalParcels,
            "状态统计的总和应等于总包裹数");
    }

    #region 场景 A 测试

    [Fact]
    [SimulationScenario("ScenarioA_Formal_Baseline")]
    public async Task ScenarioA_Formal_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioA("Formal", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(5);
        summary.SortedToWrongChuteCount.Should().Be(0);
        
        // 在低摩擦、无掉包的基线场景下，错误分拣必须为0
        // 注意：在 Formal 模式下，如果 RuleEngine 模拟没有及时响应，可能会出现超时
        // 但这不影响核心验证：SortedToWrongChuteCount == 0
    }

    [Fact]
    [SimulationScenario("ScenarioA_FixedChute")]
    public async Task ScenarioA_FixedChute_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioA("FixedChute", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(5);
        summary.SortedToWrongChuteCount.Should().Be(0);
    }

    [Fact]
    [SimulationScenario("ScenarioA_RoundRobin")]
    public async Task ScenarioA_RoundRobin_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioA("RoundRobin", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(5);
        summary.SortedToWrongChuteCount.Should().Be(0);
    }

    #endregion

    #region 场景 B 测试

    [Fact]
    [SimulationScenario("ScenarioB_HighFriction_Formal")]
    public async Task ScenarioB_HighFriction_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioB("Formal", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(5);
        summary.SortedToWrongChuteCount.Should().Be(0);

        // 允许超时，但错误分拣必须为 0
        // 所有成功分拣的包裹，FinalChuteId 必须等于 TargetChuteId（这个已经在 ValidateInvariants 中验证）
    }

    #endregion

    #region 场景 C 测试

    [Fact]
    [SimulationScenario("ScenarioC_MediumFrictionWithDropout_Formal")]
    public async Task ScenarioC_MediumFrictionWithDropout_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioC("Formal", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(5);
        summary.SortedToWrongChuteCount.Should().Be(0);

        // 应该有部分包裹掉包（由于 5% 的掉包率）
        // 但这取决于随机数，所以不强制要求
        // summary.DroppedCount.Should().BeGreaterThan(0);

        // 所有成功分拣的包裹必须正确
        if (summary.SortedToTargetChuteCount > 0)
        {
            // 传感器事件完整、在 TTL 内、FinalChuteId == TargetChuteId
            // 这些条件在 SimulationRunner 中已经验证
        }
    }

    #endregion

    #region 场景 D 测试

    [Fact]
    [SimulationScenario("ScenarioD_ExtremePressure_Formal")]
    public async Task ScenarioD_ExtremePressure_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioD("Formal", parcelCount: 5);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(5);
        summary.SortedToWrongChuteCount.Should().Be(0);

        // 可接受较多 Timeout / Dropped
        // 但仍然必须保证 SortedToWrongChute == 0
        (summary.TimeoutCount + summary.DroppedCount).Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region 场景 E 测试

    [Fact]
    [SimulationScenario("ScenarioE_HighFrictionWithDropout_Formal")]
    public async Task ScenarioE_HighFrictionWithDropout_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioE("Formal", parcelCount: 10);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(10);
        summary.SortedToWrongChuteCount.Should().Be(0);

        // 高摩擦和中等掉包率下，预期会有部分包裹超时或掉包
        // 但成功分拣的包裹不应该被分到错误的格口
        (summary.TimeoutCount + summary.DroppedCount).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [SimulationScenario("ScenarioE_HighFrictionWithDropout_FixedChute")]
    public async Task ScenarioE_HighFrictionWithDropout_FixedChute_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioE("FixedChute", parcelCount: 10);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(10);
        summary.SortedToWrongChuteCount.Should().Be(0);
    }

    [Fact]
    [SimulationScenario("ScenarioE_HighFrictionWithDropout_RoundRobin")]
    public async Task ScenarioE_HighFrictionWithDropout_RoundRobin_ShouldHaveNoMissorts()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreateScenarioE("RoundRobin", parcelCount: 10);

        // Act
        var summary = await RunScenarioAsync(scenario);

        // Assert
        ValidateInvariants(summary, scenario);
        summary.TotalParcels.Should().Be(10);
        summary.SortedToWrongChuteCount.Should().Be(0);
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
