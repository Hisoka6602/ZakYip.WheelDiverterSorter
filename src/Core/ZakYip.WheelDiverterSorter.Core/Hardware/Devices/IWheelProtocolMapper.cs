using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Hardware.Devices;

/// <summary>
/// 摆轮协议映射器接口
/// </summary>
/// <remarks>
/// 本接口属于 HAL（硬件抽象层），定义摆轮协议映射的抽象契约。
/// 
/// <para><b>职责</b>：在领域层的摆轮命令与底层协议帧之间进行转换</para>
/// <list type="bullet">
///   <item>将领域层的 <see cref="DiverterDirection"/> 等领域模型转换为协议命令</item>
///   <item>将协议响应/状态转换为领域层可理解的 <see cref="WheelCommandResult"/> 或 <see cref="WheelDeviceStatus"/></item>
/// </list>
/// <para><b>设计原则</b>：</para>
/// <para>
/// 此接口定义在 HAL 层中，不依赖具体协议实现。
/// 具体的协议映射实现（如数递鸟、莫迪等）在 Drivers 层实现，
/// 确保协议细节不渗透到领域层。
/// </para>
/// <para><b>使用示例</b>：</para>
/// <code>
/// // 在驱动层使用映射器
/// var protocolCommand = _mapper.MapDirectionToCommand(DiverterDirection.Left);
/// var frame = BuildFrame(deviceAddress, protocolCommand);
/// await SendFrameAsync(frame);
/// 
/// // 解析响应
/// var result = _mapper.MapResponseToResult(responseCode, responseBytes);
/// </code>
/// </remarks>
public interface IWheelProtocolMapper
{
    /// <summary>
    /// 摆轮协议映射器的厂商标识
    /// </summary>
    /// <remarks>
    /// 用于标识此映射器所适配的摆轮厂商/协议类型
    /// </remarks>
    string VendorName { get; }

    /// <summary>
    /// 将领域层的摆轮方向映射为协议层命令
    /// </summary>
    /// <param name="direction">领域层的摆轮方向</param>
    /// <returns>协议层的命令对象</returns>
    /// <remarks>
    /// 实现类应将 DiverterDirection 枚举值转换为对应协议的命令码。
    /// 例如，数递鸟协议中，Left 对应 0x53，Right 对应 0x55，Straight 对应 0x54。
    /// </remarks>
    WheelProtocolCommand MapDirectionToCommand(DiverterDirection direction);

    /// <summary>
    /// 构建协议帧数据
    /// </summary>
    /// <param name="deviceAddress">设备地址</param>
    /// <param name="command">协议命令</param>
    /// <returns>完整的协议帧字节数组</returns>
    /// <remarks>
    /// 根据具体协议规范构建完整的帧数据，包含起始字节、长度、校验等。
    /// </remarks>
    byte[] BuildCommandFrame(byte deviceAddress, WheelProtocolCommand command);

    /// <summary>
    /// 尝试解析协议响应帧
    /// </summary>
    /// <param name="frameData">接收到的帧数据</param>
    /// <param name="result">解析后的命令结果</param>
    /// <returns>是否解析成功</returns>
    /// <remarks>
    /// 将底层协议的响应帧解析为领域层可理解的 <see cref="WheelCommandResult"/>。
    /// 隐藏协议细节，如状态码、应答码等。
    /// </remarks>
    bool TryParseResponse(ReadOnlySpan<byte> frameData, out WheelCommandResult result);

    /// <summary>
    /// 尝试解析设备状态帧
    /// </summary>
    /// <param name="frameData">接收到的帧数据</param>
    /// <param name="status">解析后的设备状态</param>
    /// <returns>是否解析成功</returns>
    /// <remarks>
    /// 将底层协议的状态上报帧解析为领域层可理解的 <see cref="WheelDeviceStatus"/>。
    /// </remarks>
    bool TryParseDeviceStatus(ReadOnlySpan<byte> frameData, out WheelDeviceStatus status);
}

/// <summary>
/// 协议层命令（值对象）
/// </summary>
/// <remarks>
/// 本类型属于 HAL（硬件抽象层），封装协议命令的字节值和语义名称。
/// 避免在业务代码中直接使用 magic number。
/// </remarks>
public readonly record struct WheelProtocolCommand
{
    /// <summary>
    /// 命令字节值
    /// </summary>
    public byte CommandCode { get; init; }

    /// <summary>
    /// 命令的语义名称（用于日志和调试）
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// 对应的领域层方向（如果适用）
    /// </summary>
    public DiverterDirection? Direction { get; init; }

    /// <summary>
    /// 创建协议命令
    /// </summary>
    public WheelProtocolCommand(byte commandCode, string name, DiverterDirection? direction = null)
    {
        CommandCode = commandCode;
        Name = name;
        Direction = direction;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Name}(0x{CommandCode:X2})";
}

/// <summary>
/// 摆轮命令执行结果（领域层模型）
/// </summary>
/// <remarks>
/// 本类型属于 HAL（硬件抽象层），将协议层的响应码转换为领域层可理解的结果。
/// 隐藏协议细节。
/// </remarks>
public readonly record struct WheelCommandResult
{
    /// <summary>
    /// 设备地址
    /// </summary>
    public byte DeviceAddress { get; init; }

    /// <summary>
    /// 结果类型
    /// </summary>
    public WheelCommandResultType ResultType { get; init; }

    /// <summary>
    /// 执行的命令方向（如果适用）
    /// </summary>
    public DiverterDirection? Direction { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => ResultType is WheelCommandResultType.Acknowledged 
                             or WheelCommandResultType.Completed;

    /// <summary>
    /// 是否已完成（动作执行完毕）
    /// </summary>
    public bool IsCompleted => ResultType == WheelCommandResultType.Completed;

    /// <summary>
    /// 错误消息（如果有）
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 摆轮设备状态（领域层模型）
/// </summary>
/// <remarks>
/// 本类型属于 HAL（硬件抽象层），将协议层的设备状态转换为领域层可理解的状态。
/// 隐藏协议细节。
/// </remarks>
public readonly record struct WheelDeviceStatus
{
    /// <summary>
    /// 设备地址
    /// </summary>
    public byte DeviceAddress { get; init; }

    /// <summary>
    /// 设备状态
    /// </summary>
    public WheelDeviceState State { get; init; }

    /// <summary>
    /// 是否可用（可接收命令）
    /// </summary>
    public bool IsAvailable => State is WheelDeviceState.Standby or WheelDeviceState.Running;

    /// <summary>
    /// 是否处于故障状态
    /// </summary>
    public bool IsFaulted => State is WheelDeviceState.Fault or WheelDeviceState.EmergencyStop;
}
