// Global using directives for enum namespaces after reorganization
global using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
global using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
global using ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;
global using ZakYip.WheelDiverterSorter.Core.Enums.Parcel;
global using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
global using ZakYip.WheelDiverterSorter.Core.Enums.System;

// PR2: 为向后兼容导出新子命名空间的类型
// Exported types from new sub-namespaces for backward compatibility
global using ZakYip.WheelDiverterSorter.Execution.PathExecution;
global using ZakYip.WheelDiverterSorter.Execution.Diagnostics;
global using ZakYip.WheelDiverterSorter.Execution.Segments;
global using ZakYip.WheelDiverterSorter.Execution.Infrastructure;
global using ZakYip.WheelDiverterSorter.Execution.Routing;

// 已迁移到 Core 层的类型别名（向后兼容）
// Types migrated to Core layer - aliases for backward compatibility
global using ISwitchingPathExecutor = ZakYip.WheelDiverterSorter.Core.Abstractions.Execution.ISwitchingPathExecutor;
global using IWheelCommandExecutor = ZakYip.WheelDiverterSorter.Core.Abstractions.Execution.IWheelCommandExecutor;
global using WheelCommand = ZakYip.WheelDiverterSorter.Core.Abstractions.Execution.WheelCommand;
global using PathExecutionResult = ZakYip.WheelDiverterSorter.Core.Abstractions.Execution.PathExecutionResult;
