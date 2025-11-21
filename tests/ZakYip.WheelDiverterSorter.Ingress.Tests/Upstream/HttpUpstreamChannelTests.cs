using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Configuration;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Http;

namespace ZakYip.WheelDiverterSorter.Ingress.Tests.Upstream;

public class HttpUpstreamChannelTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new UpstreamChannelConfig
        {
            Name = "TestChannel",
            Type = "HTTP",
            Endpoint = "http://localhost:5000",
            TimeoutMs = 5000
        };

        // Act
        var channel = new HttpUpstreamChannel(httpClient, config);

        // Assert
        Assert.Equal("TestChannel", channel.Name);
        Assert.Equal("HTTP", channel.ChannelType);
        Assert.False(channel.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_ShouldSetIsConnectedToTrue()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new UpstreamChannelConfig
        {
            Name = "TestChannel",
            Type = "HTTP",
            Endpoint = "http://localhost:5000",
            TimeoutMs = 5000
        };
        var channel = new HttpUpstreamChannel(httpClient, config);

        // Act
        await channel.ConnectAsync();

        // Assert
        Assert.True(channel.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldSetIsConnectedToFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new UpstreamChannelConfig
        {
            Name = "TestChannel",
            Type = "HTTP",
            Endpoint = "http://localhost:5000",
            TimeoutMs = 5000
        };
        var channel = new HttpUpstreamChannel(httpClient, config);
        await channel.ConnectAsync();

        // Act
        await channel.DisconnectAsync();

        // Assert
        Assert.False(channel.IsConnected);
    }

    [Fact]
    public void Dispose_ShouldSetIsConnectedToFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new UpstreamChannelConfig
        {
            Name = "TestChannel",
            Type = "HTTP",
            Endpoint = "http://localhost:5000",
            TimeoutMs = 5000
        };
        var channel = new HttpUpstreamChannel(httpClient, config);

        // Act
        channel.Dispose();

        // Assert
        Assert.False(channel.IsConnected);
    }
}
