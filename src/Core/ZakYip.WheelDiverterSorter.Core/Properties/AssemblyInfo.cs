using System.Runtime.CompilerServices;

// 允许 Configuration.Persistence 程序集访问internal成员
// 这是为了让 LiteDB 能够序列化/反序列化 RoutePlan 的 internal set 属性
[assembly: InternalsVisibleTo("ZakYip.WheelDiverterSorter.Configuration.Persistence")]
