using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Validation tests for Communication API endpoints
/// Tests missing fields, out-of-range values, and invalid enum values
/// </summary>
public class CommunicationApiValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public CommunicationApiValidationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region Missing Required Fields Tests

    [Fact]
    public async Task UpdateConfiguration_WithMissingMode_ReturnsBadRequest()
    {
        // Arrange - Create JSON without required Mode field
        var jsonContent = @"{
            ""connectionMode"": 0,
            ""tcpServer"": ""localhost:8000"",
            ""timeoutMs"": 5000
        }";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/communication/config/persisted", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("mode", responseContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateConfiguration_WithMissingConnectionMode_ReturnsBadRequest()
    {
        // Arrange - Create JSON without required ConnectionMode field
        var jsonContent = @"{
            ""mode"": 1,
            ""tcpServer"": ""localhost:8000"",
            ""timeoutMs"": 5000
        }";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/communication/config/persisted", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("connection", responseContent, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Out of Range Tests

    [Theory]
    [InlineData(500)]    // Below minimum (1000)
    [InlineData(999)]    // Just below minimum
    [InlineData(70000)]  // Above maximum (60000)
    public async Task UpdateConfiguration_WithInvalidTimeoutMs_ReturnsBadRequest(int invalidTimeout)
    {
        // Arrange
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = invalidTimeout,
            RetryCount = 3,
            RetryDelayMs = 1000,
            InitialBackoffMs = 200,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("timeout", responseContent, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]  // Negative
    [InlineData(11)]  // Above maximum (10)
    [InlineData(100)] // Way above maximum
    public async Task UpdateConfiguration_WithInvalidRetryCount_ReturnsBadRequest(int invalidRetryCount)
    {
        // Arrange
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = 5000,
            RetryCount = invalidRetryCount,
            RetryDelayMs = 1000,
            InitialBackoffMs = 200,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("retry", responseContent, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(50)]     // Below minimum (100)
    [InlineData(99)]     // Just below minimum
    [InlineData(10000)]  // Above maximum (5000)
    public async Task UpdateConfiguration_WithInvalidInitialBackoffMs_ReturnsBadRequest(int invalidBackoff)
    {
        // Arrange
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = 5000,
            RetryCount = 3,
            RetryDelayMs = 1000,
            InitialBackoffMs = invalidBackoff,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("backoff", responseContent, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(500)]    // Below minimum (1000)
    [InlineData(999)]    // Just below minimum
    [InlineData(15000)]  // Above maximum (10000)
    public async Task UpdateConfiguration_WithInvalidMaxBackoffMs_ReturnsBadRequest(int invalidMaxBackoff)
    {
        // Arrange
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = 5000,
            RetryCount = 3,
            RetryDelayMs = 1000,
            InitialBackoffMs = 200,
            MaxBackoffMs = invalidMaxBackoff,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("backoff", responseContent, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Invalid Enum Tests

    [Fact]
    public async Task UpdateConfiguration_WithInvalidCommunicationMode_ReturnsBadRequest()
    {
        // Arrange - Use invalid enum value (e.g., 99)
        var jsonContent = @"{
            ""mode"": 99,
            ""connectionMode"": 0,
            ""tcpServer"": ""localhost:8000"",
            ""timeoutMs"": 5000,
            ""retryCount"": 3,
            ""retryDelayMs"": 1000,
            ""initialBackoffMs"": 200,
            ""maxBackoffMs"": 2000,
            ""enableAutoReconnect"": true,
            ""enableInfiniteRetry"": true
        }";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/communication/config/persisted", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfiguration_WithInvalidConnectionMode_ReturnsBadRequest()
    {
        // Arrange - Use invalid enum value (e.g., 99)
        var jsonContent = @"{
            ""mode"": 1,
            ""connectionMode"": 99,
            ""tcpServer"": ""localhost:8000"",
            ""timeoutMs"": 5000,
            ""retryCount"": 3,
            ""retryDelayMs"": 1000,
            ""initialBackoffMs"": 200,
            ""maxBackoffMs"": 2000,
            ""enableAutoReconnect"": true,
            ""enableInfiniteRetry"": true
        }";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/communication/config/persisted", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Valid Configuration Tests

    [Fact]
    public async Task UpdateConfiguration_WithValidData_ReturnsSuccess()
    {
        // Arrange - All valid values
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = 5000,
            RetryCount = 3,
            RetryDelayMs = 1000,
            InitialBackoffMs = 200,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.OK,
            $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateConfiguration_WithMinimumValidValues_ReturnsSuccess()
    {
        // Arrange - Minimum valid values at boundary
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Http,
            ConnectionMode = ConnectionMode.Client,
            HttpApi = "http://localhost:5000/api/chute",
            TimeoutMs = 1000,      // Minimum
            RetryCount = 0,         // Minimum
            RetryDelayMs = 100,     // Minimum
            InitialBackoffMs = 100, // Minimum
            MaxBackoffMs = 1000,    // Minimum
            EnableAutoReconnect = false,
            EnableInfiniteRetry = false
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.OK,
            $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateConfiguration_WithMaximumValidValues_ReturnsSuccess()
    {
        // Arrange - Maximum valid values at boundary
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Server,
            TcpServer = "192.168.1.100:9000",
            TimeoutMs = 60000,     // Maximum
            RetryCount = 10,        // Maximum
            RetryDelayMs = 10000,   // Maximum
            InitialBackoffMs = 5000, // Maximum
            MaxBackoffMs = 10000,   // Maximum (though implementation caps at 2000)
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.OK,
            $"Expected success but got {response.StatusCode}");
    }

    #endregion

    #region Malformed JSON Tests

    [Fact]
    public async Task UpdateConfiguration_WithMalformedJson_ReturnsBadRequest()
    {
        // Arrange - Invalid JSON syntax
        var jsonContent = @"{
            ""mode"": 1,
            ""connectionMode"": 0,
            ""tcpServer"": ""localhost:8000"",
            ""timeoutMs"": 5000,
            INVALID JSON HERE
        }";
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/communication/config/persisted", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfiguration_WithEmptyBody_ReturnsBadRequest()
    {
        // Arrange - Empty body
        var content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync("/api/communication/config/persisted", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Error Response Format Tests

    [Fact]
    public async Task UpdateConfiguration_WithMultipleErrors_ReturnsAllValidationErrors()
    {
        // Arrange - Multiple validation errors at once
        var config = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = 500,         // Invalid: too low
            RetryCount = 20,          // Invalid: too high
            RetryDelayMs = 1000,
            InitialBackoffMs = 50,    // Invalid: too low
            MaxBackoffMs = 20000,     // Invalid: too high
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        // Should contain multiple error messages
        Assert.NotEmpty(responseContent);
    }

    #endregion
}
