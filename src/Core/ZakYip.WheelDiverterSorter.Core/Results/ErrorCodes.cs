namespace ZakYip.WheelDiverterSorter.Core.Results;

/// <summary>
/// 统一错误码定义
/// Unified error codes for the wheel diverter sorter system
/// </summary>
/// <remarks>
/// <para>
/// 错误码格式: {Category}_{SubCategory}_{ErrorType}
/// - Category: 错误所属的模块类别
/// - SubCategory: 具体子模块（可选）
/// - ErrorType: 错误类型
/// </para>
/// <para>
/// 使用建议：
/// - 业务流程中使用 switch (result.ErrorCode) 处理不同错误
/// - 日志和监控中使用错误码进行统计和分类
/// - UI 层可基于错误码显示本地化的错误信息
/// </para>
/// </remarks>
public static class ErrorCodes
{
    #region 通用错误 / General Errors

    /// <summary>
    /// 未知错误
    /// </summary>
    public const string Unknown = "UNKNOWN";

    /// <summary>
    /// 操作超时
    /// </summary>
    public const string Timeout = "TIMEOUT";

    /// <summary>
    /// 操作被取消
    /// </summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>
    /// 参数无效
    /// </summary>
    public const string InvalidParameter = "INVALID_PARAMETER";

    /// <summary>
    /// 资源未找到
    /// </summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>
    /// 配置错误
    /// </summary>
    public const string ConfigurationError = "CONFIGURATION_ERROR";

    #endregion

    #region 上游通信错误 / Upstream Communication Errors

    /// <summary>
    /// 上游连接失败
    /// </summary>
    public const string UpstreamConnectionFailed = "UPSTREAM_CONNECTION_FAILED";

    /// <summary>
    /// 上游响应超时
    /// </summary>
    public const string UpstreamTimeout = "UPSTREAM_TIMEOUT";

    /// <summary>
    /// 上游发送失败
    /// </summary>
    public const string UpstreamSendFailed = "UPSTREAM_SEND_FAILED";

    /// <summary>
    /// 上游协议错误
    /// </summary>
    public const string UpstreamProtocolError = "UPSTREAM_PROTOCOL_ERROR";

    /// <summary>
    /// 上游响应无效
    /// </summary>
    public const string UpstreamInvalidResponse = "UPSTREAM_INVALID_RESPONSE";

    /// <summary>
    /// 上游未连接
    /// </summary>
    public const string UpstreamNotConnected = "UPSTREAM_NOT_CONNECTED";

    #endregion

    #region 摆轮驱动错误 / Wheel Diverter Driver Errors

    /// <summary>
    /// 摆轮命令执行超时
    /// </summary>
    public const string WheelCommandTimeout = "WHEEL_COMMAND_TIMEOUT";

    /// <summary>
    /// 摆轮命令执行失败
    /// </summary>
    public const string WheelCommandFailed = "WHEEL_COMMAND_FAILED";

    /// <summary>
    /// 摆轮未找到
    /// </summary>
    public const string WheelNotFound = "WHEEL_NOT_FOUND";

    /// <summary>
    /// 摆轮通信错误
    /// </summary>
    public const string WheelCommunicationError = "WHEEL_COMMUNICATION_ERROR";

    /// <summary>
    /// 摆轮硬件故障
    /// </summary>
    public const string WheelHardwareFailure = "WHEEL_HARDWARE_FAILURE";

    /// <summary>
    /// 摆轮方向无效
    /// </summary>
    public const string WheelInvalidDirection = "WHEEL_INVALID_DIRECTION";

    #endregion

    #region 路径执行错误 / Path Execution Errors

    /// <summary>
    /// 路径生成失败
    /// </summary>
    public const string PathGenerationFailed = "PATH_GENERATION_FAILED";

    /// <summary>
    /// 路径段执行失败
    /// </summary>
    public const string PathSegmentFailed = "PATH_SEGMENT_FAILED";

    /// <summary>
    /// 路径段执行超时
    /// </summary>
    public const string PathSegmentTimeout = "PATH_SEGMENT_TIMEOUT";

    /// <summary>
    /// 路径验证失败（节点不健康）
    /// </summary>
    public const string PathValidationFailed = "PATH_VALIDATION_FAILED";

    /// <summary>
    /// 路径超载（时间预算不足）
    /// </summary>
    public const string PathOverloaded = "PATH_OVERLOADED";

    #endregion

    #region 格口分配错误 / Chute Assignment Errors

    /// <summary>
    /// 格口分配超时
    /// </summary>
    public const string ChuteAssignmentTimeout = "CHUTE_ASSIGNMENT_TIMEOUT";

    /// <summary>
    /// 格口分配失败
    /// </summary>
    public const string ChuteAssignmentFailed = "CHUTE_ASSIGNMENT_FAILED";

    /// <summary>
    /// 格口不存在于拓扑
    /// </summary>
    public const string ChuteNotInTopology = "CHUTE_NOT_IN_TOPOLOGY";

    /// <summary>
    /// 格口满载
    /// </summary>
    public const string ChuteFull = "CHUTE_FULL";

    #endregion

    #region 包裹处理错误 / Parcel Processing Errors

    /// <summary>
    /// 包裹未找到
    /// </summary>
    public const string ParcelNotFound = "PARCEL_NOT_FOUND";

    /// <summary>
    /// 包裹创建失败
    /// </summary>
    public const string ParcelCreationFailed = "PARCEL_CREATION_FAILED";

    /// <summary>
    /// 包裹状态无效
    /// </summary>
    public const string ParcelInvalidState = "PARCEL_INVALID_STATE";

    /// <summary>
    /// 重复包裹检测
    /// </summary>
    public const string ParcelDuplicate = "PARCEL_DUPLICATE";

    #endregion

    #region 系统状态错误 / System State Errors

    /// <summary>
    /// 系统未就绪
    /// </summary>
    public const string SystemNotReady = "SYSTEM_NOT_READY";

    /// <summary>
    /// 系统繁忙
    /// </summary>
    public const string SystemBusy = "SYSTEM_BUSY";

    /// <summary>
    /// 系统拥堵
    /// </summary>
    public const string SystemCongested = "SYSTEM_CONGESTED";

    /// <summary>
    /// 系统状态无效
    /// </summary>
    public const string SystemInvalidState = "SYSTEM_INVALID_STATE";

    #endregion

    #region IO / 传感器错误 / IO and Sensor Errors

    /// <summary>
    /// 传感器读取失败
    /// </summary>
    public const string SensorReadFailed = "SENSOR_READ_FAILED";

    /// <summary>
    /// 传感器未找到
    /// </summary>
    public const string SensorNotFound = "SENSOR_NOT_FOUND";

    /// <summary>
    /// IO操作失败
    /// </summary>
    public const string IoOperationFailed = "IO_OPERATION_FAILED";

    #endregion

    /// <summary>
    /// 根据错误码获取错误描述（用于日志和调试）
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <returns>错误描述</returns>
    public static string GetDescription(string errorCode)
    {
        return errorCode switch
        {
            Unknown => "未知错误",
            Timeout => "操作超时",
            Cancelled => "操作被取消",
            InvalidParameter => "参数无效",
            NotFound => "资源未找到",
            ConfigurationError => "配置错误",

            UpstreamConnectionFailed => "上游连接失败",
            UpstreamTimeout => "上游响应超时",
            UpstreamSendFailed => "上游发送失败",
            UpstreamProtocolError => "上游协议错误",
            UpstreamInvalidResponse => "上游响应无效",
            UpstreamNotConnected => "上游未连接",

            WheelCommandTimeout => "摆轮命令执行超时",
            WheelCommandFailed => "摆轮命令执行失败",
            WheelNotFound => "摆轮未找到",
            WheelCommunicationError => "摆轮通信错误",
            WheelHardwareFailure => "摆轮硬件故障",
            WheelInvalidDirection => "摆轮方向无效",

            PathGenerationFailed => "路径生成失败",
            PathSegmentFailed => "路径段执行失败",
            PathSegmentTimeout => "路径段执行超时",
            PathValidationFailed => "路径验证失败",
            PathOverloaded => "路径超载",

            ChuteAssignmentTimeout => "格口分配超时",
            ChuteAssignmentFailed => "格口分配失败",
            ChuteNotInTopology => "格口不存在于拓扑",
            ChuteFull => "格口满载",

            ParcelNotFound => "包裹未找到",
            ParcelCreationFailed => "包裹创建失败",
            ParcelInvalidState => "包裹状态无效",
            ParcelDuplicate => "重复包裹检测",

            SystemNotReady => "系统未就绪",
            SystemBusy => "系统繁忙",
            SystemCongested => "系统拥堵",
            SystemInvalidState => "系统状态无效",

            SensorReadFailed => "传感器读取失败",
            SensorNotFound => "传感器未找到",
            IoOperationFailed => "IO操作失败",

            _ => $"错误码: {errorCode}"
        };
    }
}
