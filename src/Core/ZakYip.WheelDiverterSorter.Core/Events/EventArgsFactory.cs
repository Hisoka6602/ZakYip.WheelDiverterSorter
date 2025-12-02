using System.Runtime.CompilerServices;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Events;

/// <summary>
/// 事件载荷工厂类
/// PR-PERF-EVENTS01: 提供事件载荷的工厂方法，减少重复构造，支持零分配常见场景
/// </summary>
/// <remarks>
/// <para>此类为高频事件载荷提供静态工厂方法：</para>
/// <list type="bullet">
///   <item>常见的"空载荷"事件</item>
///   <item>默认状态事件</item>
///   <item>组合事件（如轴状态 + EMC锁状态）</item>
/// </list>
/// <para>工厂方法尽量内联，不引入新的抽象层。</para>
/// </remarks>
file static class EventArgsFactory
{
    #region Hardware Events

    /// <summary>
    /// 创建 IO 端口变化事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IoPortChangedEventArgs CreateIoPortChanged(
        string groupName,
        int portNumber,
        bool isOn,
        DateTimeOffset timestamp) =>
        new()
        {
            GroupName = groupName,
            PortNumber = portNumber,
            IsOn = isOn,
            Timestamp = timestamp
        };

    /// <summary>
    /// 创建摆轮状态变化事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WheelDiverterStateChangedEventArgs CreateWheelDiverterStateChanged(
        string deviceId,
        WheelDiverterState newState,
        WheelDiverterState? previousState,
        DateTimeOffset timestamp) =>
        new()
        {
            DeviceId = deviceId,
            NewState = newState,
            PreviousState = previousState,
            Timestamp = timestamp
        };

    /// <summary>
    /// 创建摆轮方向变化事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DiverterDirectionChangedEventArgs CreateDiverterDirectionChanged(
        int deviceId,
        DiverterDirection direction,
        DateTimeOffset changedAt) =>
        new()
        {
            DeviceId = deviceId,
            Direction = direction,
            ChangedAt = changedAt
        };

    #endregion

    #region Sensor Events

    /// <summary>
    /// 创建包裹检测事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ParcelDetectedEventArgs CreateParcelDetected(
        long parcelId,
        DateTimeOffset detectedAt,
        string sensorId,
        SensorType sensorType) =>
        new()
        {
            ParcelId = parcelId,
            DetectedAt = detectedAt,
            SensorId = sensorId,
            SensorType = sensorType
        };

    /// <summary>
    /// 创建包裹扫描事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ParcelScannedEventArgs CreateParcelScanned(
        string barcode,
        DateTimeOffset scannedAt) =>
        new()
        {
            Barcode = barcode,
            ScannedAt = scannedAt
        };

    #endregion

    #region Sorting Events

    /// <summary>
    /// 创建包裹创建事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ParcelCreatedEventArgs CreateParcelCreated(
        long parcelId,
        DateTimeOffset createdAt,
        string? barcode = null,
        string? sensorId = null) =>
        new()
        {
            ParcelId = parcelId,
            CreatedAt = createdAt,
            Barcode = barcode,
            SensorId = sensorId
        };

    /// <summary>
    /// 创建上游分配事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UpstreamAssignedEventArgs CreateUpstreamAssigned(
        long parcelId,
        long chuteId,
        DateTimeOffset assignedAt,
        double latencyMs = 0,
        string status = "",
        string source = "") =>
        new()
        {
            ParcelId = parcelId,
            ChuteId = chuteId,
            AssignedAt = assignedAt,
            LatencyMs = latencyMs,
            Status = status,
            Source = source
        };

    /// <summary>
    /// 创建路径规划完成事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RoutePlannedEventArgs CreateRoutePlanned(
        long parcelId,
        long targetChuteId,
        DateTimeOffset plannedAt,
        int segmentCount = 0,
        double estimatedTimeMs = 0,
        bool isHealthy = true,
        string? unhealthyNodes = null) =>
        new()
        {
            ParcelId = parcelId,
            TargetChuteId = targetChuteId,
            PlannedAt = plannedAt,
            SegmentCount = segmentCount,
            EstimatedTimeMs = estimatedTimeMs,
            IsHealthy = isHealthy,
            UnhealthyNodes = unhealthyNodes
        };

    /// <summary>
    /// 创建包裹正常落格事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ParcelDivertedEventArgs CreateParcelDiverted(
        long parcelId,
        DateTimeOffset divertedAt,
        long actualChuteId,
        long targetChuteId = 0,
        double totalTimeMs = 0) =>
        new()
        {
            ParcelId = parcelId,
            DivertedAt = divertedAt,
            ActualChuteId = actualChuteId,
            TargetChuteId = targetChuteId,
            TotalTimeMs = totalTimeMs
        };

    /// <summary>
    /// 创建吐件计划事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EjectPlannedEventArgs CreateEjectPlanned(
        long parcelId,
        DateTimeOffset plannedAt,
        string nodeId,
        string direction,
        long targetChuteId = 0) =>
        new()
        {
            ParcelId = parcelId,
            PlannedAt = plannedAt,
            NodeId = nodeId,
            Direction = direction,
            TargetChuteId = targetChuteId
        };

    /// <summary>
    /// 创建吐件指令发出事件参数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EjectIssuedEventArgs CreateEjectIssued(
        long parcelId,
        DateTimeOffset issuedAt,
        string nodeId,
        string direction,
        int commandSequence = 0) =>
        new()
        {
            ParcelId = parcelId,
            IssuedAt = issuedAt,
            NodeId = nodeId,
            Direction = direction,
            CommandSequence = commandSequence
        };

    #endregion
}
