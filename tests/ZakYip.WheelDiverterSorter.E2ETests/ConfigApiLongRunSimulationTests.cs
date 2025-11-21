using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Host.Controllers;
using ZakYip.WheelDiverterSorter.Host.IntegrationTests;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Results;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Simulation.Services;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 配置 API 覆盖检查 + 通过 API 配置后的长时间仿真E2E测试
/// </summary>
/// <remarks>
/// 完整的 API 驱动流程：
/// 1. 通过 API 配置仿真参数
/// 2. 通过 API 配置系统参数（格口、拓扑）
/// 3. 通过 API 模拟面板启动按钮
/// 4. 运行 LongRunDenseFlow 场景（1000 个包裹，每 300ms 创建一个）
/// 5. 验证所有包裹正确分拣（正常包裹落目标格口，异常包裹落异常口 21）
/// </remarks>
public class ConfigApiLongRunSimulationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IRuleEngineClient> _mockRuleEngineClient;
    private readonly Random _targetChuteRandom;
    private readonly string _testOutputDirectory;

    public ConfigApiLongRunSimulationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        
        // 使用固定种子的随机数生成器，确保可重复
        _targetChuteRandom = new Random(42);

        // 创建临时测试输出目录
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), "api-sim-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testOutputDirectory);

        // 创建模拟 RuleEngine 客户端
        _mockRuleEngineClient = new Mock<IRuleEngineClient>(MockBehavior.Loose);
        _mockRuleEngineClient.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRuleEngineClient.Setup(x => x.DisconnectAsync())
            .Returns(Task.CompletedTask);
        _mockRuleEngineClient.Setup(x => x.IsConnected)
            .Returns(true);

        // 设置包裹检测通知的默认行为：随机返回 1-20 之间的格口ID
        _mockRuleEngineClient
            .Setup(x => x.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long parcelId, CancellationToken ct) =>
            {
                // 同步触发格口分配事件，目标格口在 1-20 之间随机
                var chuteId = _targetChuteRandom.Next(1, 21); // 1-20
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
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // 添加核心配置仓储（使用模拟）
        var mockRouteRepo = new Mock<IRouteConfigurationRepository>();
        mockRouteRepo.Setup(x => x.GetByChuteId(It.IsAny<int>()))
            .Returns((int chuteId) =>
            {
                if (chuteId >= 1 && chuteId <= 21)
                {
                    return new ChuteRouteConfiguration
                    {
                        ChuteId = chuteId,
                        ChuteName = $"Chute {chuteId}",
                        DiverterConfigurations = new List<DiverterConfigurationEntry>
                        {
                            new DiverterConfigurationEntry
                            {
                                DiverterId = chuteId <= 10 ? chuteId : 10, // 模拟 10 台摆轮
                                TargetDirection = DiverterDirection.Straight,
                                SequenceNumber = 1
                            }
                        },
                        IsEnabled = true
                    };
                }
                return null;
            });

        var mockSystemConfigRepo = new Mock<ISystemConfigurationRepository>();
        mockSystemConfigRepo.Setup(x => x.Get())
            .Returns(SystemConfiguration.GetDefault());

        services.AddSingleton(mockRouteRepo.Object);
        services.AddSingleton(mockSystemConfigRepo.Object);
        services.AddSingleton(_mockRuleEngineClient.Object);

        // 添加核心服务
        services.AddSingleton<ISwitchingPathGenerator, DefaultSwitchingPathGenerator>();
        services.AddSingleton<ISwitchingPathExecutor, MockSwitchingPathExecutor>();
        services.AddSingleton<ParcelTimelineFactory>();
        services.AddSingleton<ParcelTimelineCollector>();
        services.AddSingleton<ISimulationReportWriter>(sp => 
            new MarkdownReportWriter(sp.GetRequiredService<ILogger<MarkdownReportWriter>>(), _testOutputDirectory));
        services.AddSingleton<IParcelLifecycleLogger>(sp => sp.GetRequiredService<ParcelTimelineCollector>());
        services.AddSingleton<SimulationReportPrinter>();
        services.AddSingleton<PrometheusMetrics>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 测试完整的 API 驱动仿真流程（简化版，100 个包裹）
    /// </summary>
    [Fact]
    [SimulationScenario("ApiDrivenSimulation_ConfigureViaApi_VerifyResults")]
    public async Task ApiDrivenSimulation_ConfigureViaApi_ThenStartPanel_VerifyResults()
    {
        // 本测试使用 100 个包裹来加快测试速度，验证 API 流程
        const int parcelCount = 100;

        // ====== 步骤 1: 通过 API 配置仿真参数 ======
        var simulationConfigRequest = new SimulationConfigRequest
        {
            ParcelCount = parcelCount,
            LineSpeedMmps = 1000m,
            ParcelIntervalMs = 300,
            SortingMode = "RoundRobin",
            ExceptionChuteId = 21,
            IsEnableRandomFriction = true,
            IsEnableRandomDropout = false,
            FrictionMinFactor = 0.95m,
            FrictionMaxFactor = 1.05m,
            FrictionIsDeterministic = true,
            FrictionSeed = 42,
            DropoutProbability = 0.0m,
            DropoutSeed = 42,
            MinSafeHeadwayMm = 300m,
            MinSafeHeadwayTimeMs = 300,
            DenseParcelStrategy = DenseParcelStrategy.RouteToException,
            IsPreDiverterSensorFault = false,
            IsEnableSensorJitter = false,
            JitterTriggerCount = 3,
            JitterIntervalMs = 50,
            JitterProbability = 0.0m,
            IsEnableVerboseLogging = false
        };

        var simConfigResponse = await _client.PutAsJsonAsync("/api/config/simulation", simulationConfigRequest);
        simConfigResponse.StatusCode.Should().Be(HttpStatusCode.OK, "仿真配置应该成功");

        var simConfig = await simConfigResponse.Content.ReadFromJsonAsync<SimulationConfigResponse>();
        simConfig.Should().NotBeNull();
        simConfig!.ParcelCount.Should().Be(parcelCount);
        simConfig.ExceptionChuteId.Should().Be(21);

        // ====== 步骤 2: 查询系统配置（验证默认值） ======
        var systemConfigResponse = await _client.GetAsync("/api/config/system");
        systemConfigResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var systemConfig = await systemConfigResponse.Content.ReadFromJsonAsync<SystemConfigResponse>();
        systemConfig.Should().NotBeNull();

        // ====== 步骤 3: 通过 API 检查系统状态 ======
        var stateBeforeStart = await _client.GetAsync("/api/sim/panel/state");
        stateBeforeStart.StatusCode.Should().Be(HttpStatusCode.OK);

        var stateBefore = await stateBeforeStart.Content.ReadFromJsonAsync<JsonElement>();
        stateBefore.GetProperty("currentState").GetString().Should().NotBeNullOrEmpty();

        // ====== 步骤 4: 通过 API 模拟面板启动按钮 ======
        var startResponse = await _client.PostAsync("/api/sim/panel/start", null);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK, "面板启动应该成功");

        var startResult = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        startResult.GetProperty("success").GetBoolean().Should().BeTrue();
        startResult.GetProperty("currentState").GetString().Should().Be("Running");

        // ====== 步骤 5: 运行仿真（使用已配置的参数） ======
        // 获取运行时配置
        var runtimeOptions = SimulationConfigController.GetRuntimeOptions();
        runtimeOptions.Should().NotBeNull("应该有运行时配置");

        // 创建场景并运行
        var scenario = new SimulationScenario
        {
            ScenarioName = "API-Driven-LongRunDenseFlow",
            Options = runtimeOptions!,
            Expectations = null
        };

        var summary = await RunScenarioAsync(scenario);

        // ====== 步骤 6: 验证结果 ======
        
        // 验证包裹总数
        summary.TotalParcels.Should().Be(parcelCount);
        summary.Parcels.Should().HaveCount(parcelCount);

        // 验证每个包裹都有最终状态
        foreach (var parcel in summary.Parcels)
        {
            parcel.Status.Should().NotBe(ParcelSimulationStatus.SortedToWrongChute, 
                $"包裹 {parcel.ParcelId} 不应该错分");
        }

        // 验证正常包裹落到目标格口（1-20）
        var successParcels = summary.Parcels.Where(p => p.Status == ParcelSimulationStatus.SortedToTargetChute);
        foreach (var parcel in successParcels)
        {
            parcel.FinalChuteId.Should().BeInRange(1, 20, 
                $"正常包裹 {parcel.ParcelId} 应该落在 1-20 格口");
            parcel.FinalChuteId.Should().Be(parcel.TargetChuteId, 
                $"包裹 {parcel.ParcelId} 应该落到目标格口");
        }

        // 验证异常包裹落到异常口 21
        var exceptionParcels = summary.Parcels.Where(p => 
            p.Status == ParcelSimulationStatus.TooCloseToSort ||
            p.Status == ParcelSimulationStatus.SensorFault ||
            p.Status == ParcelSimulationStatus.Timeout ||
            p.Status == ParcelSimulationStatus.ExecutionError ||
            p.Status == ParcelSimulationStatus.UnknownSource);

        foreach (var parcel in exceptionParcels)
        {
            parcel.FinalChuteId.Should().Be(21, 
                $"异常包裹 {parcel.ParcelId} (状态: {parcel.Status}) 应该落到 21 号异常口");
        }

        // 验证没有错分
        summary.SortedToWrongChuteCount.Should().Be(0, "不应该有错分包裹");

        // ====== 步骤 7: 通过 API 停止系统 ======
        var stopResponse = await _client.PostAsync("/api/sim/panel/stop", null);
        stopResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var stopResult = await stopResponse.Content.ReadFromJsonAsync<JsonElement>();
        stopResult.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// 测试获取仿真配置 API
    /// </summary>
    [Fact]
    [SimulationScenario("ConfigAPI_GetSimulationConfig_ReturnConfiguration")]
    public async Task GetSimulationConfig_ShouldReturnConfiguration()
    {
        // Act
        var response = await _client.GetAsync("/api/config/simulation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<SimulationConfigResponse>();
        config.Should().NotBeNull();
    }

    /// <summary>
    /// 测试面板状态查询 API
    /// </summary>
    [Fact]
    [SimulationScenario("ConfigAPI_GetPanelState_ReturnCurrentState")]
    public async Task GetPanelState_ShouldReturnCurrentState()
    {
        // Act
        var response = await _client.GetAsync("/api/sim/panel/state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        state.GetProperty("currentState").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试仿真状态查询 API
    /// </summary>
    [Fact]
    [SimulationScenario("ConfigAPI_GetSimulationStatus_ReturnStatus")]
    public async Task GetSimulationStatus_ShouldReturnStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/sim/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<SimulationStatus>();
        status.Should().NotBeNull();
    }

    /// <summary>
    /// 运行仿真场景（内部方法）
    /// </summary>
    private async Task<SimulationSummary> RunScenarioAsync(SimulationScenario scenario)
    {
        var ruleEngineClient = _mockRuleEngineClient.Object;
        var pathGenerator = _serviceProvider.GetRequiredService<ISwitchingPathGenerator>();
        var pathExecutor = _serviceProvider.GetRequiredService<ISwitchingPathExecutor>();
        var timelineFactory = _serviceProvider.GetRequiredService<ParcelTimelineFactory>();
        var reportPrinter = _serviceProvider.GetRequiredService<SimulationReportPrinter>();
        var metrics = _serviceProvider.GetRequiredService<PrometheusMetrics>();
        var logger = _serviceProvider.GetRequiredService<ILogger<SimulationRunner>>();
        var lifecycleLogger = _serviceProvider.GetRequiredService<IParcelLifecycleLogger>();

        var runner = new SimulationRunner(
            Microsoft.Extensions.Options.Options.Create(scenario.Options),
            ruleEngineClient,
            pathGenerator,
            pathExecutor,
            timelineFactory,
            reportPrinter,
            metrics,
            logger,
            lifecycleLogger
        );

        var summary = await runner.RunAsync(CancellationToken.None);
        return summary;
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _client?.Dispose();

        // 清理测试输出目录
        if (Directory.Exists(_testOutputDirectory))
        {
            try
            {
                Directory.Delete(_testOutputDirectory, true);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }
}
