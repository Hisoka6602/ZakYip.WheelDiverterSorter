using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ZakYip.Sorting.Core.Contracts;
using ZakYip.WheelDiverterSorter.Ingress.Upstream;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Configuration;

namespace ZakYip.WheelDiverterSorter.Ingress.Tests.Upstream;

public class UpstreamFacadeTests
{
    [Fact]
    public async Task AssignChuteAsync_WithSuccessfulSender_ShouldReturnSuccess()
    {
        // Arrange
        var mockChannel = new Mock<IUpstreamChannel>();
        mockChannel.Setup(c => c.Name).Returns("TestChannel");
        mockChannel.Setup(c => c.IsConnected).Returns(true);

        var mockSender = new Mock<IUpstreamCommandSender>();
        var expectedResponse = new AssignChuteResponse
        {
            ParcelId = 123,
            ChuteId = 5,
            IsSuccess = true,
            Source = "TestChannel"
        };

        mockSender.Setup(s => s.SendCommandAsync<AssignChuteRequest, AssignChuteResponse>(
                It.IsAny<AssignChuteRequest>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var options = Options.Create(new IngressOptions
        {
            DefaultTimeoutMs = 5000,
            EnableFallback = false
        });

        var facade = new UpstreamFacade(
            new[] { mockChannel.Object },
            new[] { mockSender.Object },
            options);

        var request = new AssignChuteRequest
        {
            ParcelId = 123,
            Barcode = "TEST123"
        };

        // Act
        var result = await facade.AssignChuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.ChuteId);
        Assert.False(result.IsFallback);
    }

    [Fact]
    public async Task AssignChuteAsync_WithFailedSender_AndFallbackEnabled_ShouldReturnFallback()
    {
        // Arrange
        var mockChannel = new Mock<IUpstreamChannel>();
        mockChannel.Setup(c => c.Name).Returns("TestChannel");
        mockChannel.Setup(c => c.IsConnected).Returns(true);

        var mockSender = new Mock<IUpstreamCommandSender>();
        mockSender.Setup(s => s.SendCommandAsync<AssignChuteRequest, AssignChuteResponse>(
                It.IsAny<AssignChuteRequest>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var options = Options.Create(new IngressOptions
        {
            DefaultTimeoutMs = 5000,
            EnableFallback = true,
            FallbackChuteId = 999
        });

        var facade = new UpstreamFacade(
            new[] { mockChannel.Object },
            new[] { mockSender.Object },
            options);

        var request = new AssignChuteRequest
        {
            ParcelId = 123,
            Barcode = "TEST123"
        };

        // Act
        var result = await facade.AssignChuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(999, result.Data.ChuteId);
        Assert.True(result.IsFallback);
        Assert.Equal("Fallback", result.Source);
    }

    [Fact]
    public async Task CreateParcelAsync_WithSuccessfulSender_ShouldReturnSuccess()
    {
        // Arrange
        var mockChannel = new Mock<IUpstreamChannel>();
        mockChannel.Setup(c => c.Name).Returns("TestChannel");
        mockChannel.Setup(c => c.IsConnected).Returns(true);

        var mockSender = new Mock<IUpstreamCommandSender>();
        var expectedResponse = new CreateParcelResponse
        {
            ParcelId = 123,
            IsSuccess = true
        };

        mockSender.Setup(s => s.SendCommandAsync<CreateParcelRequest, CreateParcelResponse>(
                It.IsAny<CreateParcelRequest>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var options = Options.Create(new IngressOptions
        {
            DefaultTimeoutMs = 5000
        });

        var facade = new UpstreamFacade(
            new[] { mockChannel.Object },
            new[] { mockSender.Object },
            options);

        var request = new CreateParcelRequest
        {
            ParcelId = 123,
            Barcode = "TEST123"
        };

        // Act
        var result = await facade.CreateParcelAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(123, result.Data.ParcelId);
    }

    [Fact]
    public void AvailableChannels_ShouldReturnAllChannelNames()
    {
        // Arrange
        var mockChannel1 = new Mock<IUpstreamChannel>();
        mockChannel1.Setup(c => c.Name).Returns("Channel1");

        var mockChannel2 = new Mock<IUpstreamChannel>();
        mockChannel2.Setup(c => c.Name).Returns("Channel2");

        var options = Options.Create(new IngressOptions());

        var facade = new UpstreamFacade(
            new[] { mockChannel1.Object, mockChannel2.Object },
            Array.Empty<IUpstreamCommandSender>(),
            options);

        // Act
        var channels = facade.AvailableChannels;

        // Assert
        Assert.Equal(2, channels.Count);
        Assert.Contains("Channel1", channels);
        Assert.Contains("Channel2", channels);
    }
}
