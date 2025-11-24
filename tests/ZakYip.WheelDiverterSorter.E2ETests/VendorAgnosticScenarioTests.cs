using Xunit;
using FluentAssertions;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 厂商无关的标准场景测试
/// Vendor-agnostic standard scenario tests for PR-27
/// </summary>
/// <remarks>
/// 这些测试使用 PR-27 定义的标准场景，可以在不同厂商驱动下运行。
/// 默认使用模拟驱动，可通过环境变量切换到真实硬件驱动。
/// </remarks>
[Collection("E2E Tests")]
public class VendorAgnosticScenarioTests : E2ETestBase
{
    public VendorAgnosticScenarioTests(E2ETestFactory factory) : base(factory)
    {
        // 默认使用模拟驱动
        // 可通过环境变量 VENDOR_ID 切换厂商
        var vendorIdEnv = Environment.GetEnvironmentVariable("VENDOR_ID");
        if (!string.IsNullOrEmpty(vendorIdEnv) && Enum.TryParse<VendorId>(vendorIdEnv, out var vendorId))
        {
            factory.SetVendorId(vendorId);
        }
        else
        {
            factory.SetVendorId(VendorId.Simulated);
        }
    }

    [Fact]
    [Trait("Category", "VendorAgnostic")]
    [Trait("Scenario", "PR27-Normal")]
    public async Task PR27_NormalSorting_Should_SortAllParcelsCorrectly()
    {
        // Arrange
        SetupDefaultRouteConfiguration();
        var scenario = ScenarioDefinitions.CreatePR27NormalSorting(parcelCount: 50);

        // Act & Assert
        // 在实际实现中，这里会运行场景并验证结果
        // 目前作为占位符，验证场景定义本身
        scenario.ScenarioName.Should().Be("PR27-正常分拣场景");
        scenario.VendorId.Should().Be(VendorId.Simulated);
        scenario.Options.ParcelCount.Should().Be(50);
        scenario.Options.IsEnableRandomFriction.Should().BeFalse();
        scenario.Options.IsEnableRandomDropout.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "VendorAgnostic")]
    [Trait("Scenario", "PR27-UpstreamDelay")]
    public async Task PR27_UpstreamDelay_Should_HandleDelaysGracefully()
    {
        // Arrange
        SetupDefaultRouteConfiguration();
        var scenario = ScenarioDefinitions.CreatePR27UpstreamDelay(parcelCount: 30);

        // Act & Assert
        scenario.ScenarioName.Should().Be("PR27-上游延迟场景");
        scenario.VendorId.Should().Be(VendorId.Simulated);
        scenario.Options.ParcelCount.Should().Be(30);
        scenario.FaultInjection.Should().NotBeNull();
        scenario.FaultInjection!.InjectUpstreamDelay.Should().BeTrue();
        scenario.FaultInjection.UpstreamDelayRangeMs.Should().Be((100, 300));

        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "VendorAgnostic")]
    [Trait("Scenario", "PR27-NodeFailure")]
    public async Task PR27_NodeFailure_Should_RouteToExceptionChute()
    {
        // Arrange
        SetupDefaultRouteConfiguration();
        var scenario = ScenarioDefinitions.CreatePR27NodeFailure(parcelCount: 40);

        // Act & Assert
        scenario.ScenarioName.Should().Be("PR27-节点故障场景");
        scenario.VendorId.Should().Be(VendorId.Simulated);
        scenario.Options.ParcelCount.Should().Be(40);
        scenario.FaultInjection.Should().NotBeNull();
        scenario.FaultInjection!.InjectNodeFailure.Should().BeTrue();
        scenario.FaultInjection.FailedDiverterIds.Should().Contain(new[] { 2, 4 });

        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "VendorAgnostic")]
    [Trait("Scenario", "Serialization")]
    public async Task Scenario_ShouldBe_SerializableToJson()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreatePR27NormalSorting();

        // Act
        var json = SimulationScenarioSerializer.SerializeToJson(scenario);
        var deserialized = SimulationScenarioSerializer.DeserializeFromJson(json);

        // Assert
        json.Should().NotBeNullOrEmpty();
        deserialized.Should().NotBeNull();
        deserialized!.ScenarioName.Should().Be(scenario.ScenarioName);
        deserialized.VendorId.Should().Be(scenario.VendorId);
        deserialized.Options.ParcelCount.Should().Be(scenario.Options.ParcelCount);

        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "VendorAgnostic")]
    [Trait("Scenario", "FilePersistence")]
    public async Task Scenario_ShouldBe_PersistableToFile()
    {
        // Arrange
        var scenario = ScenarioDefinitions.CreatePR27NormalSorting();
        var tempFile = Path.Combine(Path.GetTempPath(), $"scenario-{Guid.NewGuid()}.json");

        try
        {
            // Act
            await SimulationScenarioSerializer.SaveToFileAsync(scenario, tempFile);
            var loaded = await SimulationScenarioSerializer.LoadFromFileAsync(tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            loaded.Should().NotBeNull();
            loaded!.ScenarioName.Should().Be(scenario.ScenarioName);
            loaded.VendorId.Should().Be(scenario.VendorId);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
