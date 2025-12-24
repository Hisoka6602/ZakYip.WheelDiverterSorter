using System.Runtime.CompilerServices;

// 允许 Configuration.Persistence 程序集访问internal成员
// 用于 LiteDB 序列化/反序列化配置模型的 internal set 属性
[assembly: InternalsVisibleTo("ZakYip.WheelDiverterSorter.Configuration.Persistence")]
