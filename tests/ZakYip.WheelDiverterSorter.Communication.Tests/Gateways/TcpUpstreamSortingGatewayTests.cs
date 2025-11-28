using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Exceptions;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Adapters;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Gateways;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Gateways;

/// <summary>
/// TcpUpstreamSortingGateway 单元测试
/// </summary>
public class TcpUpstreamSortingGatewayTests
{
    private readonly Mock<IRuleEngineClient> _mockClient;
    private readonly IUpstreamContractMapper _mapper;
    private readonly Mock<ILogger<TcpUpstreamSortingGateway>> _mockLogger;
    private readonly RuleEngineConnectionOptions _options;
    private readonly TcpUpstreamSortingGateway _gateway;

    public TcpUpstreamSortingGatewayTests()
    {
        _mockClient = new Mock<IRuleEngineClient>();
        _mapper = new DefaultUpstreamContractMapper();
        _mockLogger = new Mock<ILogger<TcpUpstreamSortingGateway>>();
        _options = new RuleEngineConnectionOptions
        {
            Mode = CommunicationMode.Tcp,
            TcpServer = "localhost:5000",
            TimeoutMs = 5000,
            RetryCount = 3
        };

        _gateway = new TcpUpstreamSortingGateway(
            _mockClient.Object,
            _mapper,
            _mockLogger.Object,
            _options);
    }

    [Fact]
    public async Task RequestSortingAsync_WhenSuccessful_ReturnsValidResponse()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 123456789,
            Barcode = "TEST001"
        };

        _mockClient.Setup(x => x.IsConnected).Returns(true);
        _mockClient.Setup(x => x.NotifyParcelDetectedAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .Callback<long, CancellationToken>((parcelId, ct) =>
            {
                // Simulate the event being raised
                var eventArgs = new ChuteAssignmentNotificationEventArgs
                {
                    ParcelId = parcelId,
                    ChuteId = 5,
                    NotificationTime = DateTimeOffset.Now
                };
                _mockClient.Raise(x => x.ChuteAssignmentReceived += null, _mockClient.Object, eventArgs);
            })
            .ReturnsAsync(true);

        // Act
        var result = await _gateway.RequestSortingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123456789, result.ParcelId);
        Assert.Equal(5, result.TargetChuteId);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsException);
        Assert.Equal("SUCCESS", result.ReasonCode);
    }

    [Fact]
    public async Task RequestSortingAsync_WhenClientNotConnected_ConnectsFirst()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 123456789
        };

        _mockClient.SetupSequence(x => x.IsConnected)
            .Returns(false)
            .Returns(true);

        _mockClient.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockClient.Setup(x => x.NotifyParcelDetectedAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .Callback<long, CancellationToken>((parcelId, ct) =>
            {
                // Simulate the event being raised
                var eventArgs = new ChuteAssignmentNotificationEventArgs
                {
                    ParcelId = parcelId,
                    ChuteId = 1,
                    NotificationTime = DateTimeOffset.Now
                };
                _mockClient.Raise(x => x.ChuteAssignmentReceived += null, _mockClient.Object, eventArgs);
            })
            .ReturnsAsync(true);

        // Act
        var result = await _gateway.RequestSortingAsync(request);

        // Assert
        _mockClient.Verify(x => x.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RequestSortingAsync_WhenConnectionFails_ThrowsUpstreamUnavailableException()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 123456789
        };

        _mockClient.Setup(x => x.IsConnected).Returns(false);
        _mockClient.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => _gateway.RequestSortingAsync(request));
    }

    [Fact]
    public async Task RequestSortingAsync_WhenNotifyFails_ThrowsUpstreamUnavailableException()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 123456789
        };

        _mockClient.Setup(x => x.IsConnected).Returns(true);
        _mockClient.Setup(x => x.NotifyParcelDetectedAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => _gateway.RequestSortingAsync(request));
    }

    [Fact]
    public async Task RequestSortingAsync_WhenCancelled_ThrowsUpstreamUnavailableException()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 123456789
        };

        _mockClient.Setup(x => x.IsConnected).Returns(true);
        _mockClient.Setup(x => x.NotifyParcelDetectedAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => _gateway.RequestSortingAsync(request));
    }
}
