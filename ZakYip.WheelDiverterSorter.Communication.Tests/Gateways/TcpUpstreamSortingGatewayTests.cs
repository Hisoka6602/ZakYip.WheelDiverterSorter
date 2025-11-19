using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Exceptions;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Gateways;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Gateways;

/// <summary>
/// TcpUpstreamSortingGateway 单元测试
/// </summary>
public class TcpUpstreamSortingGatewayTests
{
    private readonly Mock<IRuleEngineClient> _mockClient;
    private readonly Mock<ILogger<TcpUpstreamSortingGateway>> _mockLogger;
    private readonly RuleEngineConnectionOptions _options;
    private readonly TcpUpstreamSortingGateway _gateway;

    public TcpUpstreamSortingGatewayTests()
    {
        _mockClient = new Mock<IRuleEngineClient>();
        _mockLogger = new Mock<ILogger<TcpUpstreamSortingGateway>>();
        _options = new RuleEngineConnectionOptions
        {
            Mode = Core.Enums.CommunicationMode.Tcp,
            TcpServer = "localhost:5000",
            TimeoutMs = 5000,
            RetryCount = 3
        };

        _gateway = new TcpUpstreamSortingGateway(
            _mockClient.Object,
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

        var expectedResponse = new ChuteAssignmentResponse
        {
            ParcelId = 123456789,
            ChuteId = 5,
            IsSuccess = true,
            ResponseTime = DateTimeOffset.UtcNow
        };

        _mockClient.Setup(x => x.IsConnected).Returns(true);
        #pragma warning disable CS0618
        _mockClient.Setup(x => x.RequestChuteAssignmentAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);
        #pragma warning restore CS0618

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

        #pragma warning disable CS0618
        _mockClient.Setup(x => x.RequestChuteAssignmentAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChuteAssignmentResponse
            {
                ParcelId = 123456789,
                ChuteId = 1,
                IsSuccess = true
            });
        #pragma warning restore CS0618

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
    public async Task RequestSortingAsync_WhenResponseIsNull_ThrowsInvalidResponseException()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 123456789
        };

        _mockClient.Setup(x => x.IsConnected).Returns(true);
        #pragma warning disable CS0618
        _mockClient.Setup(x => x.RequestChuteAssignmentAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChuteAssignmentResponse)null!);
        #pragma warning restore CS0618

        // Act & Assert
        await Assert.ThrowsAsync<InvalidResponseException>(
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
        #pragma warning disable CS0618
        _mockClient.Setup(x => x.RequestChuteAssignmentAsync(
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        #pragma warning restore CS0618

        // Act & Assert
        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => _gateway.RequestSortingAsync(request));
    }
}
