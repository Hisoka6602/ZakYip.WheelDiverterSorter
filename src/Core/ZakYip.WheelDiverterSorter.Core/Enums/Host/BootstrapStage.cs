using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Host;

/// <summary>
/// 启动过程阶段枚举
/// Bootstrap Stage Enumeration
/// </summary>
/// <remarks>
/// 定义系统启动过程的各个阶段，用于启动流程监控和仿真测试。
/// Defines the stages of system startup process for monitoring and simulation testing.
/// PR-40: 支持启动过程高级仿真场景
/// </remarks>
public enum BootstrapStage
{
    /// <summary>未启动：系统尚未开始启动流程</summary>
    [Description("未启动")]
    NotStarted = 0,

    /// <summary>配置加载中：正在加载配置和装配依赖注入</summary>
    [Description("配置加载中")]
    Bootstrapping = 1,

    /// <summary>驱动初始化中：驱动握手、自检</summary>
    [Description("驱动初始化中")]
    DriversInitializing = 2,

    /// <summary>通讯连接中：与 RuleEngine 建立连接</summary>
    [Description("通讯连接中")]
    CommunicationConnecting = 3,

    /// <summary>健康检查中：核心模块就绪性检查</summary>
    [Description("健康检查中")]
    HealthChecking = 4,

    /// <summary>就绪稳定：所有核心模块 Ready，可接单</summary>
    [Description("就绪稳定")]
    HealthStable = 5,

    /// <summary>启动失败：启动过程中发生不可恢复错误</summary>
    [Description("启动失败")]
    Failed = 99
}
