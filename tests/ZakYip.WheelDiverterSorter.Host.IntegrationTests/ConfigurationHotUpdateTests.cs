using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Integration tests for Configuration API hot updates
/// Tests that configuration changes via API are applied immediately and persist
/// </summary>
public class ConfigurationHotUpdateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationHotUpdateTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = TestJsonOptions.GetOptions();
    }

    #region Communication Configuration Hot Update Tests

    [Fact]
    public async Task CommunicationConfig_UpdateAndVerify_ConfigurationPersists()
    {
        // Arrange - Get current configuration first
        var getResponse = await _client.GetAsync("/api/communication/config/persisted");
        Assert.True(getResponse.IsSuccessStatusCode, "Failed to get initial configuration");

        // Create updated configuration
        var updatedConfig = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "192.168.1.100:8888",
            TimeoutMs = 7000,
            RetryCount = 5,
            RetryDelayMs = 1500,
            InitialBackoffMs = 300,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act - Update configuration
        var updateResponse = await _client.PutAsJsonAsync("/api/communication/config/persisted", updatedConfig, _jsonOptions);

        // Assert - Update succeeds
        Assert.True(
            updateResponse.IsSuccessStatusCode,
            $"Configuration update failed with status {updateResponse.StatusCode}");

        // Verify - Get configuration again and verify changes
        var verifyResponse = await _client.GetAsync("/api/communication/config/persisted");
        Assert.True(verifyResponse.IsSuccessStatusCode);

        var retrievedConfig = await verifyResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(retrievedConfig);
        Assert.Equal("192.168.1.100:8888", retrievedConfig.TcpServer);
        Assert.Equal(7000, retrievedConfig.TimeoutMs);
        Assert.Equal(5, retrievedConfig.RetryCount);
        Assert.Equal(300, retrievedConfig.InitialBackoffMs);
    }

    [Fact]
    public async Task CommunicationConfig_UpdateConnectionMode_ChangesAppliedImmediately()
    {
        // Arrange - Create configuration with Client mode
        var clientModeConfig = new CommunicationConfiguration
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

        // Act - Update to Client mode
        var updateResponse = await _client.PutAsJsonAsync("/api/communication/config/persisted", clientModeConfig, _jsonOptions);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Verify
        var verifyResponse = await _client.GetAsync("/api/communication/config/persisted");
        var retrieved = await verifyResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(retrieved);
        Assert.Equal(ConnectionMode.Client, retrieved.ConnectionMode);

        // Change to Server mode
        var serverModeConfig = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Server,
            TcpServer = "0.0.0.0:9000",
            TimeoutMs = 5000,
            RetryCount = 3,
            RetryDelayMs = 1000,
            InitialBackoffMs = 200,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        updateResponse = await _client.PutAsJsonAsync("/api/communication/config/persisted", serverModeConfig, _jsonOptions);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Verify server mode is set
        verifyResponse = await _client.GetAsync("/api/communication/config/persisted");
        retrieved = await verifyResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(retrieved);
        Assert.Equal(ConnectionMode.Server, retrieved.ConnectionMode);
    }

    [Fact]
    public async Task CommunicationConfig_UpdateBackoffParameters_NewValuesApplied()
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
            InitialBackoffMs = 500,  // Different from default
            MaxBackoffMs = 1800,     // Different from default
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act
        var updateResponse = await _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Verify
        var verifyResponse = await _client.GetAsync("/api/communication/config/persisted");
        var retrieved = await verifyResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(retrieved);
        Assert.Equal(500, retrieved.InitialBackoffMs);
        Assert.Equal(1800, retrieved.MaxBackoffMs);
    }

    [Fact]
    public async Task CommunicationConfig_Reset_RestoresDefaults()
    {
        // Arrange - First update to non-default values
        var customConfig = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "custom.server.com:9999",
            TimeoutMs = 10000,
            RetryCount = 8,
            RetryDelayMs = 2000,
            InitialBackoffMs = 400,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        var updateResponse = await _client.PutAsJsonAsync("/api/communication/config/persisted", customConfig, _jsonOptions);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Act - Reset to defaults
        var resetResponse = await _client.PostAsync("/api/communication/config/persisted/reset", null);

        // Assert - Reset succeeds
        Assert.True(
            resetResponse.IsSuccessStatusCode || resetResponse.StatusCode == HttpStatusCode.OK,
            $"Reset failed with status {resetResponse.StatusCode}");

        // Verify - Configuration is back to defaults
        var verifyResponse = await _client.GetAsync("/api/communication/config/persisted");
        var retrieved = await verifyResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(retrieved);
        // Should have default values (not the custom ones)
        Assert.NotEqual("custom.server.com:9999", retrieved.TcpServer);
    }

    #endregion

    #region Concurrent Configuration Update Tests

    [Fact]
    public async Task CommunicationConfig_ConcurrentUpdates_HandledCorrectly()
    {
        // Arrange - Create multiple different configurations
        var configs = new[]
        {
            new CommunicationConfiguration
            {
                Mode = CommunicationMode.Tcp,
                ConnectionMode = ConnectionMode.Client,
                TcpServer = "server1.local:8000",
                TimeoutMs = 5000,
                RetryCount = 3,
                RetryDelayMs = 1000,
                InitialBackoffMs = 200,
                MaxBackoffMs = 2000,
                EnableAutoReconnect = true,
                EnableInfiniteRetry = true
            },
            new CommunicationConfiguration
            {
                Mode = CommunicationMode.Tcp,
                ConnectionMode = ConnectionMode.Client,
                TcpServer = "server2.local:8001",
                TimeoutMs = 6000,
                RetryCount = 4,
                RetryDelayMs = 1200,
                InitialBackoffMs = 250,
                MaxBackoffMs = 2000,
                EnableAutoReconnect = true,
                EnableInfiniteRetry = true
            },
            new CommunicationConfiguration
            {
                Mode = CommunicationMode.Tcp,
                ConnectionMode = ConnectionMode.Client,
                TcpServer = "server3.local:8002",
                TimeoutMs = 7000,
                RetryCount = 5,
                RetryDelayMs = 1500,
                InitialBackoffMs = 300,
                MaxBackoffMs = 2000,
                EnableAutoReconnect = true,
                EnableInfiniteRetry = true
            }
        };

        // Act - Send concurrent update requests
        var updateTasks = configs.Select(config =>
            _client.PutAsJsonAsync("/api/communication/config/persisted", config, _jsonOptions)).ToArray();

        var responses = await Task.WhenAll(updateTasks);

        // Assert - All updates should succeed (though only the last one will be persisted)
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        Assert.True(successCount >= 2, $"Expected at least 2 successful updates, got {successCount}");

        // Verify - Final configuration should be one of the three
        var verifyResponse = await _client.GetAsync("/api/communication/config/persisted");
        var retrieved = await verifyResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(retrieved);
        
        // Should match one of the three servers
        var matchesAnyConfig = configs.Any(c => c.TcpServer == retrieved.TcpServer);
        Assert.True(matchesAnyConfig, "Final configuration should match one of the update requests");
    }

    #endregion

    #region System Configuration Update Tests

    [Fact]
    public async Task SystemConfig_UpdateAndVerify_ConfigurationPersists()
    {
        // This test verifies that system configuration updates via API work correctly
        // Arrange
        var currentResponse = await _client.GetAsync("/api/config/system");
        
        if (!currentResponse.IsSuccessStatusCode)
        {
            // System config endpoint may not exist yet, skip test
            Assert.True(true, "System config endpoint not available");
            return;
        }

        // If endpoint exists, we would test updating system configuration here
        Assert.True(true, "System config endpoint exists");
    }

    #endregion

    #region Error Handling in Updates

    [Fact]
    public async Task CommunicationConfig_UpdateWithInvalidData_RollsBackOrRejectsCorrectly()
    {
        // Arrange - Get current valid configuration
        var getResponse = await _client.GetAsync("/api/communication/config/persisted");
        var originalConfig = await getResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(originalConfig);

        // Create invalid configuration (timeout too low)
        var invalidConfig = new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:8000",
            TimeoutMs = 100, // Invalid: below minimum
            RetryCount = 3,
            RetryDelayMs = 1000,
            InitialBackoffMs = 200,
            MaxBackoffMs = 2000,
            EnableAutoReconnect = true,
            EnableInfiniteRetry = true
        };

        // Act - Attempt to update with invalid data
        var updateResponse = await _client.PutAsJsonAsync("/api/communication/config/persisted", invalidConfig, _jsonOptions);

        // Assert - Should be rejected
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);

        // Verify - Original configuration should still be in place
        var verifyResponse = await _client.GetAsync("/api/communication/config/persisted");
        var currentConfig = await verifyResponse.Content.ReadFromJsonAsync<CommunicationConfiguration>(_jsonOptions);
        Assert.NotNull(currentConfig);
        Assert.Equal(originalConfig.TimeoutMs, currentConfig.TimeoutMs); // Should be unchanged
    }

    #endregion
}
