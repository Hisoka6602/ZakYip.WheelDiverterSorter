using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.ShuDiNiao;

/// <summary>
/// 数递鸟摆轮协议映射器实现
/// </summary>
/// <remarks>
/// <para>负责在领域层摆轮命令与数递鸟协议帧之间进行转换。</para>
/// <para>将所有数递鸟协议细节（魔数、状态码、帧结构等）封装在此类中，
/// 确保协议细节不渗透到 Execution/Core 层。</para>
/// </remarks>
public sealed class ShuDiNiaoWheelProtocolMapper : IWheelProtocolMapper
{
    /// <inheritdoc/>
    public string VendorName => "ShuDiNiao";

    /// <inheritdoc/>
    public WheelProtocolCommand MapDirectionToCommand(DiverterDirection direction)
    {
        return direction switch
        {
            DiverterDirection.Left => new WheelProtocolCommand(
                (byte)ShuDiNiaoControlCommand.TurnLeft,
                "左摆",
                DiverterDirection.Left),
            
            DiverterDirection.Right => new WheelProtocolCommand(
                (byte)ShuDiNiaoControlCommand.TurnRight,
                "右摆",
                DiverterDirection.Right),
            
            DiverterDirection.Straight => new WheelProtocolCommand(
                (byte)ShuDiNiaoControlCommand.ReturnCenter,
                "回中",
                DiverterDirection.Straight),
            
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                $"不支持的摆轮方向: {direction}")
        };
    }

    /// <inheritdoc/>
    public byte[] BuildCommandFrame(byte deviceAddress, WheelProtocolCommand command)
    {
        // 将通用命令转换为数递鸟特定命令
        var shuDiNiaoCommand = (ShuDiNiaoControlCommand)command.CommandCode;
        return ShuDiNiaoProtocol.BuildCommandFrame(deviceAddress, shuDiNiaoCommand);
    }

    /// <inheritdoc/>
    public bool TryParseResponse(ReadOnlySpan<byte> frameData, out WheelCommandResult result)
    {
        result = default;

        if (!ShuDiNiaoProtocol.TryParseResponse(frameData, out var deviceAddress, out var responseCode))
        {
            return false;
        }

        result = MapResponseCodeToResult(deviceAddress, responseCode);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParseDeviceStatus(ReadOnlySpan<byte> frameData, out WheelDeviceStatus status)
    {
        status = default;

        if (!ShuDiNiaoProtocol.TryParseDeviceStatus(frameData, out var deviceAddress, out var deviceState))
        {
            return false;
        }

        status = MapDeviceStateToStatus(deviceAddress, deviceState);
        return true;
    }

    /// <summary>
    /// 将数递鸟响应码映射为领域层命令结果
    /// </summary>
    private static WheelCommandResult MapResponseCodeToResult(byte deviceAddress, ShuDiNiaoResponseCode responseCode)
    {
        var (resultType, direction) = responseCode switch
        {
            // 应答类
            ShuDiNiaoResponseCode.RunAck => (WheelCommandResultType.Acknowledged, (DiverterDirection?)null),
            ShuDiNiaoResponseCode.StopAck => (WheelCommandResultType.Acknowledged, null),
            ShuDiNiaoResponseCode.TurnLeftAck => (WheelCommandResultType.Acknowledged, DiverterDirection.Left),
            ShuDiNiaoResponseCode.ReturnCenterAck => (WheelCommandResultType.Acknowledged, DiverterDirection.Straight),
            ShuDiNiaoResponseCode.TurnRightAck => (WheelCommandResultType.Acknowledged, DiverterDirection.Right),
            
            // 完成类
            ShuDiNiaoResponseCode.TurnLeftComplete => (WheelCommandResultType.Completed, DiverterDirection.Left),
            ShuDiNiaoResponseCode.ReturnCenterComplete => (WheelCommandResultType.Completed, DiverterDirection.Straight),
            ShuDiNiaoResponseCode.TurnRightComplete => (WheelCommandResultType.Completed, DiverterDirection.Right),
            
            _ => (WheelCommandResultType.Unknown, null)
        };

        return new WheelCommandResult
        {
            DeviceAddress = deviceAddress,
            ResultType = resultType,
            Direction = direction
        };
    }

    /// <summary>
    /// 将数递鸟设备状态映射为领域层设备状态
    /// </summary>
    private static WheelDeviceStatus MapDeviceStateToStatus(byte deviceAddress, ShuDiNiaoDeviceState deviceState)
    {
        var state = deviceState switch
        {
            ShuDiNiaoDeviceState.Standby => WheelDeviceState.Standby,
            ShuDiNiaoDeviceState.Running => WheelDeviceState.Running,
            ShuDiNiaoDeviceState.EmergencyStop => WheelDeviceState.EmergencyStop,
            ShuDiNiaoDeviceState.Fault => WheelDeviceState.Fault,
            _ => WheelDeviceState.Unknown
        };

        return new WheelDeviceStatus
        {
            DeviceAddress = deviceAddress,
            State = state
        };
    }

    #region 便捷方法（用于驱动层内部使用）

    /// <summary>
    /// 创建运行命令
    /// </summary>
    public static WheelProtocolCommand CreateRunCommand()
    {
        return new WheelProtocolCommand(
            (byte)ShuDiNiaoControlCommand.Run,
            "运行",
            null);
    }

    /// <summary>
    /// 创建停止命令
    /// </summary>
    public static WheelProtocolCommand CreateStopCommand()
    {
        return new WheelProtocolCommand(
            (byte)ShuDiNiaoControlCommand.Stop,
            "停止",
            null);
    }

    #endregion
}
