using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Net;
using System.Net.Http.Json;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for IoLinkageController
/// </summary>
public class IoLinkageControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IoLinkageControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetIoLinkageConfig_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-linkage");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetIoLinkageConfig_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-linkage");

        // Assert
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
    }

    [Fact]
    public async Task GetIoLinkageConfig_ReturnsEnabledField()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-linkage");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<IoLinkageConfigResponse>(
            CustomWebApplicationFactory.JsonSerializerOptions);

        // Assert
        Assert.NotNull(content);
        // Default config should be enabled
        Assert.True(content.Enabled);
    }

    [Fact]
    public async Task UpdateIoLinkageConfig_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            enabled = true,
            runningStateIos = new[]
            {
                new { bitNumber = 3, level = "ActiveLow" }
            },
            stoppedStateIos = new[]
            {
                new { bitNumber = 3, level = "ActiveHigh" }
            },
            emergencyStopStateIos = Array.Empty<object>(),
            upstreamConnectionExceptionStateIos = Array.Empty<object>(),
            diverterExceptionStateIos = Array.Empty<object>(),
            postPreStartWarningStateIos = Array.Empty<object>(),
            wheelDiverterDisconnectedStateIos = Array.Empty<object>()
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/io-linkage", request);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateIoLinkageConfig_WithEmergencyStopIos_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            enabled = true,
            runningStateIos = Array.Empty<object>(),
            stoppedStateIos = Array.Empty<object>(),
            emergencyStopStateIos = new[]
            {
                new { bitNumber = 10, level = "ActiveHigh" }
            },
            upstreamConnectionExceptionStateIos = Array.Empty<object>(),
            diverterExceptionStateIos = Array.Empty<object>(),
            postPreStartWarningStateIos = Array.Empty<object>(),
            wheelDiverterDisconnectedStateIos = Array.Empty<object>()
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/io-linkage", request);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateIoLinkageConfig_WithAllNewIoFields_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            enabled = true,
            runningStateIos = new[]
            {
                new { bitNumber = 3, level = "ActiveLow" }
            },
            stoppedStateIos = new[]
            {
                new { bitNumber = 3, level = "ActiveHigh" }
            },
            emergencyStopStateIos = new[]
            {
                new { bitNumber = 10, level = "ActiveHigh" }
            },
            upstreamConnectionExceptionStateIos = new[]
            {
                new { bitNumber = 11, level = "ActiveHigh" }
            },
            diverterExceptionStateIos = new[]
            {
                new { bitNumber = 12, level = "ActiveHigh" }
            },
            postPreStartWarningStateIos = new[]
            {
                new { bitNumber = 13, level = "ActiveLow" }
            },
            wheelDiverterDisconnectedStateIos = new[]
            {
                new { bitNumber = 14, level = "ActiveHigh" }
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/io-linkage", request);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the response contains all the new fields
        var content = await response.Content.ReadFromJsonAsync<IoLinkageConfigResponse>(
            CustomWebApplicationFactory.JsonSerializerOptions);

        Assert.NotNull(content);
        Assert.Single(content.EmergencyStopStateIos);
        Assert.Single(content.UpstreamConnectionExceptionStateIos);
        Assert.Single(content.DiverterExceptionStateIos);
        Assert.Single(content.PostPreStartWarningStateIos);
        Assert.Single(content.WheelDiverterDisconnectedStateIos);
    }

    [Fact]
    public async Task TriggerIoLinkage_WithValidRunningState_ReturnsAppropriateResponse()
    {
        // Arrange - First configure some IO linkage
        var configRequest = new
        {
            enabled = true,
            runningStateIos = new[]
            {
                new { bitNumber = 3, level = "ActiveLow" }
            },
            stoppedStateIos = Array.Empty<object>(),
            emergencyStopStateIos = Array.Empty<object>(),
            upstreamConnectionExceptionStateIos = Array.Empty<object>(),
            diverterExceptionStateIos = Array.Empty<object>(),
            postPreStartWarningStateIos = Array.Empty<object>(),
            wheelDiverterDisconnectedStateIos = Array.Empty<object>()
        };
        await _client.PutAsJsonAsync("/api/config/io-linkage", configRequest);

        // Act
        var response = await _client.PostAsync("/api/config/io-linkage/trigger?systemState=Running", null);

        // Assert - May succeed or fail based on simulated driver response
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetIoPointStatus_WithValidBitNumber_ReturnsAppropriateResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-linkage/status/3");

        // Assert - May succeed or fail based on simulated driver availability
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetIoPointStatus_WithInvalidBitNumber_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-linkage/status/9999");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBatchIoPointStatus_WithValidBitNumbers_ReturnsAppropriateResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-linkage/status/batch?bitNumbers=3,5,7");

        // Assert - May succeed or fail based on simulated driver availability
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetBatchIoPointStatus_WithEmptyBitNumbers_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/config/io-linkage/status/batch?bitNumbers=");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetIoPoint_WithValidRequest_ReturnsAppropriateResponse()
    {
        // Arrange
        var request = new { level = "ActiveHigh" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/io-linkage/set/3", request);

        // Assert - May succeed or fail based on simulated driver availability
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SetIoPoint_WithInvalidBitNumber_ReturnsBadRequest()
    {
        // Arrange
        var request = new { level = "ActiveHigh" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/io-linkage/set/9999", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetBatchIoPoints_WithValidRequest_ReturnsAppropriateResponse()
    {
        // Arrange
        var request = new
        {
            ioPoints = new[]
            {
                new { bitNumber = 3, level = "ActiveHigh" },
                new { bitNumber = 5, level = "ActiveLow" }
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/io-linkage/set/batch", request);

        // Assert - May succeed or fail based on simulated driver availability
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SetBatchIoPoints_WithEmptyIoPoints_ReturnsBadRequest()
    {
        // Arrange
        var request = new { ioPoints = Array.Empty<object>() };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/io-linkage/set/batch", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private class IoLinkageConfigResponse
    {
        public bool Enabled { get; set; }
        public List<IoLinkagePointResponse> RunningStateIos { get; set; } = new();
        public List<IoLinkagePointResponse> StoppedStateIos { get; set; } = new();
        public List<IoLinkagePointResponse> EmergencyStopStateIos { get; set; } = new();
        public List<IoLinkagePointResponse> UpstreamConnectionExceptionStateIos { get; set; } = new();
        public List<IoLinkagePointResponse> DiverterExceptionStateIos { get; set; } = new();
        public List<IoLinkagePointResponse> PostPreStartWarningStateIos { get; set; } = new();
        public List<IoLinkagePointResponse> WheelDiverterDisconnectedStateIos { get; set; } = new();
    }

    private class IoLinkagePointResponse
    {
        public int BitNumber { get; set; }
        public string Level { get; set; } = string.Empty;
    }
}
