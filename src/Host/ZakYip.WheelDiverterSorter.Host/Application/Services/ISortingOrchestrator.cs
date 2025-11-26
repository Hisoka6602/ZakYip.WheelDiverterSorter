// PR-1: 向后兼容类型别名 - 实际定义已移至 Core.Sorting.Orchestration
// 这些别名确保现有代码无需修改即可继续使用 Host.Application.Services 命名空间
global using ISortingOrchestrator = ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration.ISortingOrchestrator;
global using SortingResult = ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration.SortingResult;
