// Type aliases for backward compatibility
// ISystemClock and LocalSystemClock have been moved to Core.Utilities
global using ISystemClock = ZakYip.WheelDiverterSorter.Core.Utilities.ISystemClock;
global using LocalSystemClock = ZakYip.WheelDiverterSorter.Core.Utilities.LocalSystemClock;

namespace ZakYip.WheelDiverterSorter.Observability.Utilities;

// This file provides backward compatibility for code that references
// ZakYip.WheelDiverterSorter.Observability.Utilities.ISystemClock
// The actual implementations are now in Core.Utilities
