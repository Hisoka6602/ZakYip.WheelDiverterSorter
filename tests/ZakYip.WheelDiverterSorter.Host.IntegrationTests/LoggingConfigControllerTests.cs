using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 日志配置控制器集成测试
/// </summary>
public class LoggingConfigControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LoggingConfigControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLoggingConfig_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/logging");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoggingConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetLoggingTemplate_ShouldReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/config/logging/template");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoggingConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task UpdateLoggingConfig_ShouldSucceed_WithValidRequest()
    {
        // Arrange
        var request = new LoggingConfigRequest
        {
            EnableParcelLifecycleLog = true,
            EnableParcelTraceLog = true,
            EnablePathExecutionLog = true,
            EnableCommunicationLog = false,
            EnableDriverLog = false,
            EnablePerformanceLog = true,
            EnableAlarmLog = true,
            EnableDebugLog = false
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/config/logging", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoggingConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(request.EnableParcelLifecycleLog, result.Data!.EnableParcelLifecycleLog);
        Assert.Equal(request.EnableParcelTraceLog, result.Data.EnableParcelTraceLog);
        Assert.Equal(request.EnablePathExecutionLog, result.Data.EnablePathExecutionLog);
        Assert.Equal(request.EnableCommunicationLog, result.Data.EnableCommunicationLog);
    }

    [Fact]
    public async Task ResetLoggingConfig_ShouldSucceed()
    {
        // Act
        var response = await _client.PostAsync("/api/config/logging/reset", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoggingConfigResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }
}
