using System;
using System.Collections.Generic;

namespace ZakYip.WheelDiverterSorter.E2ETests.Simulation;

/// <summary>
/// PR-42: 仿真场景清单
/// 记录所有已定义的仿真场景ID，用于回归测试验证
/// </summary>
public static class SimulationScenariosManifest
{
    /// <summary>
    /// 所有已登记的仿真场景ID列表
    /// 每个场景必须有唯一ID，并至少有一个对应的测试方法
    /// </summary>
    public static readonly IReadOnlyList<string> AllScenarioIds = new List<string>
    {
        // === Panel Startup & Basic Sorting (面板启动和基础分拣) ===
        "Panel_Startup_SingleParcel_Normal",
        "Panel_Startup_Upstream_Delay",
        "Panel_Startup_FirstParcel_Warmup",

        // === Scenario A: Low Friction, No Dropout (低摩擦，无掉包) ===
        "ScenarioA_Formal_Baseline",
        "ScenarioA_FixedChute",
        "ScenarioA_RoundRobin",

        // === Scenario B: High Friction (高摩擦) ===
        "ScenarioB_HighFriction_Formal",

        // === Scenario C: Medium Friction with Dropout (中等摩擦 + 掉包) ===
        "ScenarioC_MediumFrictionWithDropout_Formal",

        // === Scenario D: Extreme Pressure (极限压力) ===
        "ScenarioD_ExtremePressure_Formal",

        // === Scenario E: High Friction with Dropout (高摩擦 + 掉包) ===
        "ScenarioE_HighFrictionWithDropout_Formal",
        "ScenarioE_HighFrictionWithDropout_FixedChute",
        "ScenarioE_HighFrictionWithDropout_RoundRobin",

        // === Dense Traffic Scenarios (密集流量场景) ===
        "ScenarioHD1_SlightHighDensity_RouteToException",
        "ScenarioHD2_ExtremeHighDensity_RouteToException",
        "ScenarioHD3A_HighDensity_MarkAsTimeout",
        "ScenarioHD3B_HighDensity_MarkAsDropped",
        "DenseTraffic_WithoutDenseConfiguration",

        // === Sensor Fault Scenarios (传感器故障场景) ===
        "ScenarioSF1_PreDiverterSensorFault_RouteToException",
        "ScenarioSJ1_SensorJitter_DuplicatePackages",
        "SensorFault_WithLifecycleLogging",
        "SensorFault_LifecycleLogger_RecordsEvents",

        // === Long Run Scenarios (长时运行场景) ===
        "LongRunDenseFlow_AllParcelsCompleted",
        "LongRunDenseFlow_ConcurrentParcelsWithinThreshold",
        "LongRunDenseFlow_GeneratesMarkdownReport",
        "ApiDrivenSimulation_ConfigureViaApi_VerifyResults",

        // === Parcel Sorting Workflow (包裹分拣工作流) ===
        "ParcelSorting_CompleteFlow_ValidChute",
        "ParcelSorting_PathGeneration_ValidChute",
        "ParcelSorting_PathExecution_ValidPath",
        "ParcelSorting_FallbackToException_PathGenerationFails",
        "ParcelSorting_DebugAPI_ShouldProcess",

        // === Concurrent Processing (并发处理) ===
        "Concurrent_MultipleParcels_Processed",
        "Concurrent_PathGeneration_NoRaceConditions",
        "Concurrent_PathExecution_ResourceLocking",
        "Concurrent_HighThroughput_HandleLoad",
        "Concurrent_APIRequests_HandledCorrectly",
        "Concurrent_ParcelQueue_MaintainOrder",

        // === Fault Recovery (故障恢复) ===
        "FaultRecovery_DiverterFailure_FallbackToException",
        "FaultRecovery_RuleEngineConnectionLoss_UseException",
        "FaultRecovery_SensorFailure_DetectedAndLogged",
        "FaultRecovery_CommunicationTimeout_FallbackGracefully",
        "FaultRecovery_SystemRecovery_AfterTemporaryFailure",
        "FaultRecovery_MultipleFailures_NotCrashSystem",
        "FaultRecovery_InvalidRouteConfiguration_ReturnNull",
        "FaultRecovery_PathExecutionFailure_ReportCorrectError",
        "FaultRecovery_DuplicateTrigger_HandleAsException",

        // === RuleEngine Integration (规则引擎集成) ===
        "RuleEngine_Connection_EstablishSuccessfully",
        "RuleEngine_Disconnect_CleanDisconnect",
        "RuleEngine_ParcelDetectionNotification_SentToRuleEngine",
        "RuleEngine_ChuteAssignment_ReceivedFromRuleEngine",
        "RuleEngine_ConnectionFailure_HandledGracefully",
        "RuleEngine_NotificationFailure_ReturnFalse",
        "RuleEngine_AssignmentTimeout_FallbackToException",

        // === Upstream Chute Change (上游格口变更) ===
        "UpstreamChuteChange_PlanCreated_AcceptChange",
        "UpstreamChuteChange_PlanCompleted_IgnoreChange",
        "UpstreamChuteChange_PlanExceptionRouted_IgnoreChange",
        "UpstreamChuteChange_AfterDeadline_RejectChange",
        "UpstreamChuteChange_PlanNotFound_ReturnFailure",

        // === Panel Operations (面板操作) ===
        "PanelOps_BasicFlow_StartStopReset",
        "PanelOps_FaultScenario_RedLightAndBuzzer",
        "PanelOps_EmergencyStop_Scenario",
        "PanelOps_UpstreamDisconnected_Warning",
        "PanelOps_CompleteWorkflow_StateHistory",

        // === Config API Simulation (配置API仿真) ===
        "ConfigAPI_GetSimulationConfig_ReturnConfiguration",
        "ConfigAPI_GetPanelState_ReturnCurrentState",
        "ConfigAPI_GetSimulationStatus_ReturnStatus",

        // === Vendor Agnostic Scenarios (厂商无关场景 - 占位) ===
        // Note: 这些场景可能需要特殊处理或标记为长时运行
        // 暂时保留在清单中，但可能不参与快速回归测试
    };

    /// <summary>
    /// 场景分类：正常分拣闭环场景
    /// 这些场景必须验证 Parcel-First 语义和严格时间顺序
    /// </summary>
    public static readonly IReadOnlyList<string> NormalSortingScenarios = new List<string>
    {
        "Panel_Startup_SingleParcel_Normal",
        "Panel_Startup_Upstream_Delay",
        "Panel_Startup_FirstParcel_Warmup",
        "ScenarioA_Formal_Baseline",
        "ScenarioA_FixedChute",
        "ScenarioA_RoundRobin",
        "ScenarioB_HighFriction_Formal",
        "ScenarioC_MediumFrictionWithDropout_Formal",
        "ScenarioE_HighFrictionWithDropout_Formal",
        "ScenarioE_HighFrictionWithDropout_FixedChute",
        "ScenarioE_HighFrictionWithDropout_RoundRobin",
        "ParcelSorting_CompleteFlow_ValidChute",
        "ParcelSorting_PathExecution_ValidPath",
    };

    /// <summary>
    /// 场景分类：故障和异常场景
    /// 这些场景可能出现预期的警告或错误日志
    /// </summary>
    public static readonly IReadOnlyList<string> FaultScenarios = new List<string>
    {
        "ScenarioD_ExtremePressure_Formal",
        "ScenarioSF1_PreDiverterSensorFault_RouteToException",
        "ScenarioSJ1_SensorJitter_DuplicatePackages",
        "FaultRecovery_DiverterFailure_FallbackToException",
        "FaultRecovery_RuleEngineConnectionLoss_UseException",
        "FaultRecovery_SensorFailure_DetectedAndLogged",
        "FaultRecovery_CommunicationTimeout_FallbackGracefully",
        "FaultRecovery_MultipleFailures_NotCrashSystem",
        "FaultRecovery_DuplicateTrigger_HandleAsException",
        "PanelOps_FaultScenario_RedLightAndBuzzer",
        "PanelOps_EmergencyStop_Scenario",
    };

    /// <summary>
    /// 场景分类：长时运行场景
    /// 这些场景运行时间较长，可能在 nightly 或专门的长时测试中运行
    /// </summary>
    public static readonly IReadOnlyList<string> LongRunningScenarios = new List<string>
    {
        "LongRunDenseFlow_AllParcelsCompleted",
        "LongRunDenseFlow_ConcurrentParcelsWithinThreshold",
        "LongRunDenseFlow_GeneratesMarkdownReport",
        "ApiDrivenSimulation_ConfigureViaApi_VerifyResults",
    };
}
