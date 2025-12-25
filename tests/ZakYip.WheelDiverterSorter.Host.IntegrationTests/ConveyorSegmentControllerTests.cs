using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 输送线段配置控制器集成测试
/// </summary>
public class ConveyorSegmentControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ConveyorSegmentControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllSegments_ShouldReturnEmptyList_Initially()
    {
        // Act
        var response = await _client.GetAsync("/api/config/conveyor-segments");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<ConveyorSegmentResponse>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CreateSegment_ShouldSucceed_WithValidRequest()
    {
        // Arrange
        var request = new ConveyorSegmentRequest
        {
            SegmentId = 100,
            SegmentName = "Test Segment",
            LengthMm = 5000,
            SpeedMmps = 1000m,
            TimeToleranceMs = 500,
            Remarks = "Test segment for integration testing"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/config/conveyor-segments", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data!.SegmentId);
        Assert.Equal("Test Segment", result.Data.SegmentName);
        Assert.Equal(5000, result.Data.LengthMm);
        Assert.Equal(1000m, result.Data.SpeedMmps);
        Assert.Equal(500, result.Data.TimeToleranceMs);
        Assert.Equal(5000, result.Data.CalculatedTransitTimeMs); // 5000mm / 1000mmps * 1000 = 5000ms
        Assert.Equal(5500, result.Data.CalculatedTimeoutThresholdMs); // 5000ms + 500ms = 5500ms
    }

    [Fact]
    public async Task GetSegmentById_ShouldReturnNotFound_WhenSegmentDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync("/api/config/conveyor-segments/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSegment_ShouldSucceed_WhenSegmentExists()
    {
        // Arrange - First create a segment
        var createRequest = new ConveyorSegmentRequest
        {
            SegmentId = 101,
            SegmentName = "Original Name",
            LengthMm = 4000,
            SpeedMmps = 800m,
            TimeToleranceMs = 400,
        };
        await _client.PostAsJsonAsync("/api/config/conveyor-segments", createRequest);

        // Act - Update the segment
        var updateRequest = new ConveyorSegmentRequest
        {
            SegmentId = 101,
            SegmentName = "Updated Name",
            LengthMm = 6000,
            SpeedMmps = 1200m,
            TimeToleranceMs = 600,
        };
        var response = await _client.PutAsJsonAsync("/api/config/conveyor-segments/101", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Updated Name", result.Data!.SegmentName);
        Assert.Equal(6000, result.Data.LengthMm);
        Assert.Equal(1200m, result.Data.SpeedMmps);
        Assert.Equal(5000, result.Data.CalculatedTransitTimeMs); // 6000mm / 1200mmps * 1000 = 5000ms
        Assert.Equal(5600, result.Data.CalculatedTimeoutThresholdMs); // 5000ms + 600ms = 5600ms
    }

    [Fact]
    public async Task DeleteSegment_ShouldSucceed_WhenSegmentExists()
    {
        // Arrange - First create a segment
        var createRequest = new ConveyorSegmentRequest
        {
            SegmentId = 102,
            SegmentName = "To Delete",
            LengthMm = 5000,
            SpeedMmps = 1000m,
            TimeToleranceMs = 500,
        };
        await _client.PostAsJsonAsync("/api/config/conveyor-segments", createRequest);

        // Act - Delete the segment
        var response = await _client.DeleteAsync("/api/config/conveyor-segments/102");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify it's deleted
        var getResponse = await _client.GetAsync("/api/config/conveyor-segments/102");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetTemplate_ShouldReturnDefaultConfiguration()
    {
        // Act
        var response = await _client.GetAsync("/api/config/conveyor-segments/template/999");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(999, result.Data!.SegmentId);
        Assert.Equal(5000, result.Data.LengthMm); // Default length
        Assert.Equal(1000m, result.Data.SpeedMmps); // Default speed
        Assert.Equal(500, result.Data.TimeToleranceMs); // Default tolerance
    }

    [Fact]
    public async Task CreateSegment_ShouldFail_WithInvalidRequest()
    {
        // Arrange - Invalid segment with negative length
        var request = new ConveyorSegmentRequest
        {
            SegmentId = 103,
            SegmentName = "Invalid Segment",
            LengthMm = -100, // Invalid: negative length
            SpeedMmps = 1000m,
            TimeToleranceMs = 500,
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/config/conveyor-segments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BatchCreate_ShouldSucceed_WithValidRequests()
    {
        // Arrange
        var batchRequest = new ConveyorSegmentBatchRequest
        {
            Segments = new List<ConveyorSegmentRequest>
            {
                new()
                {
                    SegmentId = 201,
                    SegmentName = "Batch Segment 1",
                    LengthMm = 5000,
                    SpeedMmps = 1000m,
                    TimeToleranceMs = 500,
                },
                new()
                {
                    SegmentId = 202,
                    SegmentName = "Batch Segment 2",
                    LengthMm = 6000,
                    SpeedMmps = 1200m,
                    TimeToleranceMs = 600,
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/config/conveyor-segments/batch", batchRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentBatchResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.SuccessCount);
        Assert.Equal(0, result.Data.FailureCount);
        Assert.True(result.Data.IsFullSuccess);
    }
}
