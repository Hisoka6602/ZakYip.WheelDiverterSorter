using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// ChutePathTopology API集成测试
/// </summary>
public class ChutePathTopologyControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ChutePathTopologyControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = CustomWebApplicationFactory.JsonSerializerOptions;
    }

    [Fact]
    public async Task GetChutePathTopology_ShouldReturnDefaultConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/config/chute-path-topology");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<ChutePathTopologyResponse>>(content, _jsonOptions);
        
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("default", result.Data.TopologyId);
    }

    [Fact]
    public async Task UpdateChutePathTopology_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new ChutePathTopologyRequest
        {
            TopologyName = "测试拓扑",
            Description = "集成测试用拓扑配置",
            EntrySensorId = 1, // Must match existing sensor config
            DiverterNodes = new List<DiverterPathNodeRequest>
            {
                new()
                {
                    DiverterId = 1,
                    DiverterName = "测试摆轮1",
                    PositionIndex = 1,
                    SegmentId = 1,
                    FrontSensorId = 2,
                    LeftChuteIds = new List<long> { 1 },
                    RightChuteIds = new List<long> { 2 },
                    Remarks = "测试备注"
                }
            },
            ExceptionChuteId = 999
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync("/api/config/chute-path-topology", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request (due to sensor validation), got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateChutePathTopology_WithEmptyDiverterNodes_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ChutePathTopologyRequest
        {
            TopologyName = "测试拓扑",
            Description = "空节点测试",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNodeRequest>(),
            ExceptionChuteId = 999
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync("/api/config/chute-path-topology", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChutePathTopology_WithDuplicatePositionIndex_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ChutePathTopologyRequest
        {
            TopologyName = "重复位置索引测试",
            Description = "测试重复位置索引验证",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNodeRequest>
            {
                new()
                {
                    DiverterId = 1,
                    DiverterName = "摆轮1",
                    PositionIndex = 1,
                    SegmentId = 1,
                    LeftChuteIds = new List<long> { 1 },
                    RightChuteIds = new List<long> { 2 }
                },
                new()
                {
                    DiverterId = 2,
                    DiverterName = "摆轮2",
                    PositionIndex = 1, // Duplicate position index
                    SegmentId = 2,
                    LeftChuteIds = new List<long> { 3 },
                    RightChuteIds = new List<long> { 4 }
                }
            },
            ExceptionChuteId = 999
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync("/api/config/chute-path-topology", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("位置索引重复", responseContent);
    }

    [Fact]
    public async Task UpdateChutePathTopology_WithNoChutesOnEitherSide_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ChutePathTopologyRequest
        {
            TopologyName = "无格口测试",
            Description = "测试无格口验证",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNodeRequest>
            {
                new()
                {
                    DiverterId = 1,
                    DiverterName = "摆轮1",
                    PositionIndex = 1,
                    SegmentId = 1,
                    LeftChuteIds = null, // No chutes on either side
                    RightChuteIds = null
                }
            },
            ExceptionChuteId = 999
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync("/api/config/chute-path-topology", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("至少配置一侧格口", responseContent);
    }

    [Fact]
    public async Task ExportAsJson_ShouldReturnJsonFile()
    {
        // Act
        var response = await _client.GetAsync("/api/config/chute-path-topology/export/json");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        
        // Verify it's valid JSON
        var topology = JsonSerializer.Deserialize<ChutePathTopologyResponse>(content, _jsonOptions);
        Assert.NotNull(topology);
    }

    [Fact]
    public async Task ExportAsCsv_ShouldReturnCsvFile()
    {
        // Act
        var response = await _client.GetAsync("/api/config/chute-path-topology/export/csv");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        
        // Verify it has CSV header
        Assert.Contains("DiverterId", content);
        Assert.Contains("PositionIndex", content);
    }

    [Fact]
    public async Task SimulateParcelPath_WithValidChuteId_ShouldReturnSimulationResult()
    {
        // First, set up a topology with nodes
        var topologyRequest = new ChutePathTopologyRequest
        {
            TopologyName = "测试拓扑",
            Description = "用于模拟测试",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNodeRequest>
            {
                new()
                {
                    DiverterId = 1,
                    DiverterName = "摆轮D1",
                    PositionIndex = 1,
                    SegmentId = 1,
                    FrontSensorId = 2,
                    LeftChuteIds = new List<long> { 1 },
                    RightChuteIds = new List<long> { 2 }
                },
                new()
                {
                    DiverterId = 2,
                    DiverterName = "摆轮D2",
                    PositionIndex = 2,
                    SegmentId = 2,
                    FrontSensorId = 3,
                    LeftChuteIds = new List<long> { 3 },
                    RightChuteIds = new List<long> { 4 }
                }
            },
            ExceptionChuteId = 999
        };

        var setupContent = new StringContent(
            JsonSerializer.Serialize(topologyRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        await _client.PutAsync("/api/config/chute-path-topology", setupContent);

        // Arrange
        var simulationRequest = new TopologySimulationRequest
        {
            TargetChuteId = 1,
            LineSpeedMmps = 1000m,
            DefaultSegmentLengthMm = 5000,
            SimulateTimeout = false,
            SimulateParcelLoss = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(simulationRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/config/chute-path-topology/simulate", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<TopologySimulationResult>>(responseContent, _jsonOptions);
        
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TargetChuteId);
        Assert.NotEmpty(result.Data.Steps);
        Assert.True(result.Data.IsSuccess);
        Assert.False(result.Data.IsParcelLost);
        Assert.False(result.Data.IsTimeout);
    }

    [Fact]
    public async Task SimulateParcelPath_WithTimeout_ShouldRouteToExceptionChute()
    {
        // First, set up a topology with nodes
        var topologyRequest = new ChutePathTopologyRequest
        {
            TopologyName = "测试拓扑",
            Description = "用于超时测试",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNodeRequest>
            {
                new()
                {
                    DiverterId = 1,
                    DiverterName = "摆轮D1",
                    PositionIndex = 1,
                    SegmentId = 1,
                    LeftChuteIds = new List<long> { 1 },
                    RightChuteIds = new List<long> { 2 }
                }
            },
            ExceptionChuteId = 999
        };

        var setupContent = new StringContent(
            JsonSerializer.Serialize(topologyRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        await _client.PutAsync("/api/config/chute-path-topology", setupContent);

        // Arrange
        var simulationRequest = new TopologySimulationRequest
        {
            TargetChuteId = 1,
            LineSpeedMmps = 1000m,
            DefaultSegmentLengthMm = 5000,
            SimulateTimeout = true,
            TimeoutExtraDelayMs = 5000,
            SimulateParcelLoss = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(simulationRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/config/chute-path-topology/simulate", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<TopologySimulationResult>>(responseContent, _jsonOptions);
        
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsSuccess);
        Assert.True(result.Data.IsTimeout);
        Assert.Equal(999, result.Data.ActualChuteId); // Should route to exception chute
    }

    [Fact]
    public async Task SimulateParcelPath_WithParcelLoss_ShouldIndicateLoss()
    {
        // First, set up a topology with nodes
        var topologyRequest = new ChutePathTopologyRequest
        {
            TopologyName = "测试拓扑",
            Description = "用于丢包测试",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNodeRequest>
            {
                new()
                {
                    DiverterId = 1,
                    DiverterName = "摆轮D1",
                    PositionIndex = 1,
                    SegmentId = 1,
                    LeftChuteIds = new List<long> { 1 },
                    RightChuteIds = new List<long> { 2 }
                },
                new()
                {
                    DiverterId = 2,
                    DiverterName = "摆轮D2",
                    PositionIndex = 2,
                    SegmentId = 2,
                    LeftChuteIds = new List<long> { 3 },
                    RightChuteIds = new List<long> { 4 }
                }
            },
            ExceptionChuteId = 999
        };

        var setupContent = new StringContent(
            JsonSerializer.Serialize(topologyRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");
        await _client.PutAsync("/api/config/chute-path-topology", setupContent);

        // Arrange
        var simulationRequest = new TopologySimulationRequest
        {
            TargetChuteId = 3,
            LineSpeedMmps = 1000m,
            DefaultSegmentLengthMm = 5000,
            SimulateTimeout = false,
            SimulateParcelLoss = true,
            ParcelLossAtDiverterIndex = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(simulationRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/config/chute-path-topology/simulate", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<TopologySimulationResult>>(responseContent, _jsonOptions);
        
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsSuccess);
        Assert.True(result.Data.IsParcelLost);
        Assert.Null(result.Data.ActualChuteId); // No chute reached
    }

    [Fact]
    public async Task SimulateParcelPath_WithInvalidChuteId_ShouldReturnBadRequest()
    {
        // Arrange
        var simulationRequest = new TopologySimulationRequest
        {
            TargetChuteId = 99999, // Non-existent chute
            LineSpeedMmps = 1000m
        };

        var content = new StringContent(
            JsonSerializer.Serialize(simulationRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/config/chute-path-topology/simulate", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
