using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.System;

/// <summary>
/// 启动阶段枚举
/// </summary>
public enum BootstrapStage
{
    /// <summary>正在启动：配置加载和依赖注入装配</summary>
    [Description("正在启动")]
    Bootstrapping = 0,

    /// <summary>驱动初始化：驱动握手和自检</summary>
    [Description("驱动初始化")]
    DriversInitializing = 1,

    /// <summary>建立通信连接：与 RuleEngine 建立连接</summary>
    [Description("建立通信连接")]
    CommunicationConnecting = 2,

    /// <summary>健康检查：核心模块就绪性检查</summary>
    [Description("健康检查")]
    HealthChecking = 3,

    /// <summary>健康稳定：所有核心模块 Ready，可接单</summary>
    [Description("健康稳定")]
    HealthStable = 4
}
