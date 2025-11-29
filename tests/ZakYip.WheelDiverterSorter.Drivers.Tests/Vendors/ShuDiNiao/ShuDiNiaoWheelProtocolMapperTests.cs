using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮协议映射器测试
/// </summary>
/// <remarks>
/// 验证协议映射器正确地在领域层命令和协议帧之间进行转换
/// </remarks>
public class ShuDiNiaoWheelProtocolMapperTests
{
    private readonly ShuDiNiaoWheelProtocolMapper _mapper;

    public ShuDiNiaoWheelProtocolMapperTests()
    {
        _mapper = new ShuDiNiaoWheelProtocolMapper();
    }

    [Fact(DisplayName = "VendorName 应返回 ShuDiNiao")]
    public void VendorName_ShouldReturnShuDiNiao()
    {
        // Assert
        Assert.Equal("ShuDiNiao", _mapper.VendorName);
    }

    #region MapDirectionToCommand 测试

    [Theory(DisplayName = "MapDirectionToCommand 应正确映射方向到协议命令")]
    [InlineData(DiverterDirection.Left, 0x53, "左摆")]
    [InlineData(DiverterDirection.Right, 0x55, "右摆")]
    [InlineData(DiverterDirection.Straight, 0x54, "回中")]
    public void MapDirectionToCommand_ShouldMapCorrectly(
        DiverterDirection direction,
        byte expectedCode,
        string expectedName)
    {
        // Act
        var command = _mapper.MapDirectionToCommand(direction);

        // Assert
        Assert.Equal(expectedCode, command.CommandCode);
        Assert.Equal(expectedName, command.Name);
        Assert.Equal(direction, command.Direction);
    }

    [Fact(DisplayName = "MapDirectionToCommand 对无效方向应抛出异常")]
    public void MapDirectionToCommand_InvalidDirection_ShouldThrow()
    {
        // Arrange
        var invalidDirection = (DiverterDirection)99;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _mapper.MapDirectionToCommand(invalidDirection));
    }

    #endregion

    #region BuildCommandFrame 测试

    [Theory(DisplayName = "BuildCommandFrame 应构建正确的协议帧")]
    [InlineData(DiverterDirection.Left, 0x51, new byte[] { 0x51, 0x52, 0x57, 0x51, 0x52, 0x53, 0xFE })]
    [InlineData(DiverterDirection.Right, 0x51, new byte[] { 0x51, 0x52, 0x57, 0x51, 0x52, 0x55, 0xFE })]
    [InlineData(DiverterDirection.Straight, 0x51, new byte[] { 0x51, 0x52, 0x57, 0x51, 0x52, 0x54, 0xFE })]
    public void BuildCommandFrame_ShouldBuildCorrectFrame(
        DiverterDirection direction,
        byte deviceAddress,
        byte[] expectedFrame)
    {
        // Arrange
        var command = _mapper.MapDirectionToCommand(direction);

        // Act
        var frame = _mapper.BuildCommandFrame(deviceAddress, command);

        // Assert
        Assert.Equal(expectedFrame, frame);
    }

    [Theory(DisplayName = "BuildCommandFrame 应正确设置不同设备地址")]
    [InlineData(0x51)]
    [InlineData(0x52)]
    [InlineData(0x53)]
    public void BuildCommandFrame_ShouldSetDeviceAddressCorrectly(byte deviceAddress)
    {
        // Arrange
        var command = _mapper.MapDirectionToCommand(DiverterDirection.Left);

        // Act
        var frame = _mapper.BuildCommandFrame(deviceAddress, command);

        // Assert
        Assert.Equal(deviceAddress, frame[3]);
    }

    #endregion

    #region TryParseResponse 测试

    [Theory(DisplayName = "TryParseResponse 应正确解析应答帧")]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, 0x53, 0xFE }, WheelCommandResultType.Acknowledged, DiverterDirection.Left)]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, 0x54, 0xFE }, WheelCommandResultType.Acknowledged, DiverterDirection.Straight)]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, 0x55, 0xFE }, WheelCommandResultType.Acknowledged, DiverterDirection.Right)]
    public void TryParseResponse_Acknowledged_ShouldParseCorrectly(
        byte[] frameData,
        WheelCommandResultType expectedResultType,
        DiverterDirection expectedDirection)
    {
        // Act
        var success = _mapper.TryParseResponse(frameData, out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedResultType, result.ResultType);
        Assert.Equal(expectedDirection, result.Direction);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsCompleted);
    }

    [Theory(DisplayName = "TryParseResponse 应正确解析完成帧")]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, 0x56, 0xFE }, WheelCommandResultType.Completed, DiverterDirection.Left)]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, 0x57, 0xFE }, WheelCommandResultType.Completed, DiverterDirection.Straight)]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x53, 0x58, 0xFE }, WheelCommandResultType.Completed, DiverterDirection.Right)]
    public void TryParseResponse_Completed_ShouldParseCorrectly(
        byte[] frameData,
        WheelCommandResultType expectedResultType,
        DiverterDirection expectedDirection)
    {
        // Act
        var success = _mapper.TryParseResponse(frameData, out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedResultType, result.ResultType);
        Assert.Equal(expectedDirection, result.Direction);
        Assert.True(result.IsSuccess);
        Assert.True(result.IsCompleted);
    }

    [Fact(DisplayName = "TryParseResponse 对无效帧应返回 false")]
    public void TryParseResponse_InvalidFrame_ShouldReturnFalse()
    {
        // Arrange
        var invalidFrame = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Act
        var success = _mapper.TryParseResponse(invalidFrame, out var result);

        // Assert
        Assert.False(success);
        Assert.Equal(default, result);
    }

    #endregion

    #region TryParseDeviceStatus 测试

    [Theory(DisplayName = "TryParseDeviceStatus 应正确解析设备状态")]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0x50, 0xFE }, WheelDeviceState.Standby)]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0x51, 0xFE }, WheelDeviceState.Running)]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0x52, 0xFE }, WheelDeviceState.EmergencyStop)]
    [InlineData(new byte[] { 0x51, 0x52, 0x57, 0x51, 0x51, 0x53, 0xFE }, WheelDeviceState.Fault)]
    public void TryParseDeviceStatus_ShouldParseCorrectly(
        byte[] frameData,
        WheelDeviceState expectedState)
    {
        // Act
        var success = _mapper.TryParseDeviceStatus(frameData, out var status);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedState, status.State);
        Assert.Equal(0x51, status.DeviceAddress);
    }

    [Fact(DisplayName = "TryParseDeviceStatus 对无效帧应返回 false")]
    public void TryParseDeviceStatus_InvalidFrame_ShouldReturnFalse()
    {
        // Arrange
        var invalidFrame = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Act
        var success = _mapper.TryParseDeviceStatus(invalidFrame, out var status);

        // Assert
        Assert.False(success);
        Assert.Equal(default, status);
    }

    [Theory(DisplayName = "设备状态 IsAvailable 属性应正确反映可用性")]
    [InlineData(WheelDeviceState.Standby, true)]
    [InlineData(WheelDeviceState.Running, true)]
    [InlineData(WheelDeviceState.EmergencyStop, false)]
    [InlineData(WheelDeviceState.Fault, false)]
    [InlineData(WheelDeviceState.Unknown, false)]
    public void DeviceStatus_IsAvailable_ShouldReflectCorrectly(
        WheelDeviceState state,
        bool expectedAvailable)
    {
        // Arrange
        var status = new WheelDeviceStatus { DeviceAddress = 0x51, State = state };

        // Assert
        Assert.Equal(expectedAvailable, status.IsAvailable);
    }

    [Theory(DisplayName = "设备状态 IsFaulted 属性应正确反映故障状态")]
    [InlineData(WheelDeviceState.Standby, false)]
    [InlineData(WheelDeviceState.Running, false)]
    [InlineData(WheelDeviceState.EmergencyStop, true)]
    [InlineData(WheelDeviceState.Fault, true)]
    [InlineData(WheelDeviceState.Unknown, false)]
    public void DeviceStatus_IsFaulted_ShouldReflectCorrectly(
        WheelDeviceState state,
        bool expectedFaulted)
    {
        // Arrange
        var status = new WheelDeviceStatus { DeviceAddress = 0x51, State = state };

        // Assert
        Assert.Equal(expectedFaulted, status.IsFaulted);
    }

    #endregion

    #region 便捷方法测试

    [Fact(DisplayName = "CreateRunCommand 应创建运行命令")]
    public void CreateRunCommand_ShouldCreateCorrectCommand()
    {
        // Act
        var command = ShuDiNiaoWheelProtocolMapper.CreateRunCommand();

        // Assert
        Assert.Equal(0x51, command.CommandCode);
        Assert.Equal("运行", command.Name);
        Assert.Null(command.Direction);
    }

    [Fact(DisplayName = "CreateStopCommand 应创建停止命令")]
    public void CreateStopCommand_ShouldCreateCorrectCommand()
    {
        // Act
        var command = ShuDiNiaoWheelProtocolMapper.CreateStopCommand();

        // Assert
        Assert.Equal(0x52, command.CommandCode);
        Assert.Equal("停止", command.Name);
        Assert.Null(command.Direction);
    }

    #endregion
}
