using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Adapters;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Adapters;

/// <summary>
/// 默认上游契约映射器测试
/// </summary>
/// <remarks>
/// 验证契约映射器正确地在领域层对象和上游 DTO 之间进行转换
/// </remarks>
public class DefaultUpstreamContractMapperTests
{
    private readonly DefaultUpstreamContractMapper _mapper;

    public DefaultUpstreamContractMapperTests()
    {
        _mapper = new DefaultUpstreamContractMapper();
    }

    [Fact(DisplayName = "ProtocolType 应返回 Default")]
    public void ProtocolType_ShouldReturnDefault()
    {
        // Assert
        Assert.Equal(UpstreamProtocolType.Default, _mapper.ProtocolType);
    }

    #region MapToUpstreamRequest 测试

    [Fact(DisplayName = "MapToUpstreamRequest 应正确映射所有字段")]
    public void MapToUpstreamRequest_ShouldMapAllFields()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 12345,
            Barcode = "BC001",
            RequestTime = DateTimeOffset.Parse("2024-01-15T10:30:00+08:00"),
            SensorId = 1,
            CandidateChuteIds = new List<int> { 1, 2, 3 },
            Metadata = new Dictionary<string, string> { { "key", "value" } }
        };

        // Act
        var upstreamRequest = _mapper.MapToUpstreamRequest(request);

        // Assert
        Assert.Equal(request.ParcelId, upstreamRequest.ParcelId);
        Assert.Equal(request.Barcode, upstreamRequest.Barcode);
        Assert.Equal(request.RequestTime, upstreamRequest.RequestTime);
        Assert.Equal(request.SensorId, upstreamRequest.SensorId);
        Assert.Equal(request.CandidateChuteIds, upstreamRequest.CandidateChuteIds);
        Assert.Equal(request.Metadata, upstreamRequest.Metadata);
    }

    [Fact(DisplayName = "MapToUpstreamRequest 应正确处理可选字段为空")]
    public void MapToUpstreamRequest_ShouldHandleNullOptionalFields()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 12345,
            RequestTime = DateTimeOffset.Now
        };

        // Act
        var upstreamRequest = _mapper.MapToUpstreamRequest(request);

        // Assert
        Assert.Equal(request.ParcelId, upstreamRequest.ParcelId);
        Assert.Null(upstreamRequest.Barcode);
        Assert.Null(upstreamRequest.SensorId);
        Assert.Null(upstreamRequest.CandidateChuteIds);
        Assert.Null(upstreamRequest.Metadata);
    }

    [Fact(DisplayName = "MapToUpstreamRequest 对空请求应抛出异常")]
    public void MapToUpstreamRequest_NullRequest_ShouldThrow()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _mapper.MapToUpstreamRequest(null!));
        Assert.Equal("request", ex.ParamName);
    }

    #endregion

    #region MapFromUpstreamResponse 测试

    [Fact(DisplayName = "MapFromUpstreamResponse 应正确映射成功响应")]
    public void MapFromUpstreamResponse_SuccessResponse_ShouldMapCorrectly()
    {
        // Arrange
        var parcelId = 12345L;
        var response = new UpstreamSortingResponse
        {
            ParcelId = parcelId,
            ChuteId = 5,
            IsSuccess = true,
            ResponseTime = DateTimeOffset.Now,
            Metadata = new Dictionary<string, string> { { "source", "RuleEngine" } }
        };

        // Act
        var sortingResponse = _mapper.MapFromUpstreamResponse(parcelId, response);

        // Assert
        Assert.Equal(parcelId, sortingResponse.ParcelId);
        Assert.Equal(5, sortingResponse.TargetChuteId);
        Assert.True(sortingResponse.IsSuccess);
        Assert.False(sortingResponse.IsException);
        Assert.Equal("SUCCESS", sortingResponse.ReasonCode);
        Assert.Null(sortingResponse.ErrorMessage);
        Assert.Equal("Default", sortingResponse.Source);
    }

    [Fact(DisplayName = "MapFromUpstreamResponse 应正确映射失败响应")]
    public void MapFromUpstreamResponse_FailedResponse_ShouldMapCorrectly()
    {
        // Arrange
        var parcelId = 12345L;
        var response = new UpstreamSortingResponse
        {
            ParcelId = parcelId,
            ChuteId = 999, // 异常格口
            IsSuccess = false,
            ErrorMessage = "规则引擎超时",
            ResponseTime = DateTimeOffset.Now
        };

        // Act
        var sortingResponse = _mapper.MapFromUpstreamResponse(parcelId, response);

        // Assert
        Assert.Equal(parcelId, sortingResponse.ParcelId);
        Assert.Equal(999, sortingResponse.TargetChuteId);
        Assert.False(sortingResponse.IsSuccess);
        Assert.True(sortingResponse.IsException);
        Assert.Equal("UPSTREAM_FAILED", sortingResponse.ReasonCode);
        Assert.Equal("规则引擎超时", sortingResponse.ErrorMessage);
    }

    [Fact(DisplayName = "MapFromUpstreamResponse 对空响应应抛出异常")]
    public void MapFromUpstreamResponse_NullResponse_ShouldThrow()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _mapper.MapFromUpstreamResponse(12345, null!));
        Assert.Equal("response", ex.ParamName);
    }

    #endregion

    #region MapFromUpstreamNotification 测试

    [Fact(DisplayName = "MapFromUpstreamNotification 应正确映射推送通知")]
    public void MapFromUpstreamNotification_ShouldMapCorrectly()
    {
        // Arrange
        var notification = new UpstreamChuteAssignmentNotification
        {
            ParcelId = 12345,
            ChuteId = 5,
            NotificationTime = DateTimeOffset.Now,
            Source = "WebSocket",
            Metadata = new Dictionary<string, string> { { "channel", "ws-01" } }
        };

        // Act
        var sortingResponse = _mapper.MapFromUpstreamNotification(notification);

        // Assert
        Assert.Equal(notification.ParcelId, sortingResponse.ParcelId);
        Assert.Equal(notification.ChuteId, sortingResponse.TargetChuteId);
        Assert.True(sortingResponse.IsSuccess);
        Assert.False(sortingResponse.IsException);
        Assert.Equal("SUCCESS", sortingResponse.ReasonCode);
        Assert.Equal("WebSocket", sortingResponse.Source);
        Assert.Equal(notification.Metadata, sortingResponse.Metadata);
    }

    [Fact(DisplayName = "MapFromUpstreamNotification 应使用默认 Source")]
    public void MapFromUpstreamNotification_NoSource_ShouldUseDefaultSource()
    {
        // Arrange
        var notification = new UpstreamChuteAssignmentNotification
        {
            ParcelId = 12345,
            ChuteId = 5,
            NotificationTime = DateTimeOffset.Now
        };

        // Act
        var sortingResponse = _mapper.MapFromUpstreamNotification(notification);

        // Assert
        Assert.Equal("Default", sortingResponse.Source);
    }

    [Fact(DisplayName = "MapFromUpstreamNotification 对空通知应抛出异常")]
    public void MapFromUpstreamNotification_NullNotification_ShouldThrow()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _mapper.MapFromUpstreamNotification(null!));
        Assert.Equal("notification", ex.ParamName);
    }

    #endregion

    #region CreateFallbackResponse 测试

    [Fact(DisplayName = "CreateFallbackResponse 应创建正确的降级响应")]
    public void CreateFallbackResponse_ShouldCreateCorrectResponse()
    {
        // Arrange
        var parcelId = 12345L;
        var fallbackChuteId = 999L;
        var reasonCode = "TIMEOUT";
        var errorMessage = "上游通讯超时";

        // Act
        var response = _mapper.CreateFallbackResponse(parcelId, fallbackChuteId, reasonCode, errorMessage);

        // Assert
        Assert.Equal(parcelId, response.ParcelId);
        Assert.Equal(fallbackChuteId, response.TargetChuteId);
        Assert.True(response.IsSuccess); // 降级也是一种成功处理
        Assert.True(response.IsException);
        Assert.Equal(reasonCode, response.ReasonCode);
        Assert.Equal(errorMessage, response.ErrorMessage);
        Assert.Equal("Fallback", response.Source);
    }

    [Fact(DisplayName = "CreateFallbackResponse 应正确处理空错误消息")]
    public void CreateFallbackResponse_NullErrorMessage_ShouldHandle()
    {
        // Act
        var response = _mapper.CreateFallbackResponse(12345, 999, "UNKNOWN", null);

        // Assert
        Assert.Null(response.ErrorMessage);
    }

    #endregion
}
