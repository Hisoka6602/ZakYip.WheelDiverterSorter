using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;

/// <summary>
/// LiteDB BsonMapper 配置助手类
/// </summary>
/// <remarks>
/// 配置 LiteDB 的序列化行为，确保枚举以字符串形式存储而不是整数，
/// 并配置配置模型的 Id 属性作为 BsonId
/// </remarks>
public static class LiteDbMapperConfig
{
    /// <summary>
    /// 配置 BsonMapper 以支持枚举的字符串序列化
    /// </summary>
    /// <param name="mapper">要配置的 BsonMapper 实例</param>
    public static void ConfigureEnumAsString(BsonMapper mapper)
    {
        // 配置全局枚举序列化为字符串
        mapper.EnumAsInteger = false;
    }

    /// <summary>
    /// 配置配置模型的 Id 属性作为 BsonId
    /// </summary>
    /// <param name="mapper">要配置的 BsonMapper 实例</param>
    public static void ConfigureEntityIds(BsonMapper mapper)
    {
        // 配置各配置模型的 Id 属性为 BsonId（自动递增）
        mapper.Entity<SystemConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<IoLinkageConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<LoggingConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<PanelConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<ChuteRouteConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<CommunicationConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<DriverConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<SensorConfiguration>()
            .Id(x => x.Id);
        
        mapper.Entity<WheelDiverterConfiguration>()
            .Id(x => x.Id);
        
        // 配置 ConveyorSegmentConfiguration 实体
        // 数据库中 _id 字段为 ObjectId 类型，不映射到任何 C# 属性
        // 业务逻辑使用 SegmentId 作为唯一标识，已在 SegmentId 上创建唯一索引
#pragma warning disable CS0618 // Type or member is obsolete
        mapper.Entity<ConveyorSegmentConfiguration>()
            .Ignore(x => x.Id);  // 忽略 Id 字段，不映射到 _id
#pragma warning restore CS0618 // Type or member is obsolete
        
        // 配置 RoutePlan 实体（使用 ParcelId 作为主键）
        // 忽略 DomainEvents 属性（这是内存中的领域事件，不需要持久化）
        mapper.Entity<RoutePlan>()
            .Id(x => x.ParcelId)
            .Ignore(x => x.DomainEvents);
        
        // WheelBindingsConfig removed - wheel binding now handled via topology + vendor config association
    }

    /// <summary>
    /// 配置嵌套类型的序列化支持
    /// </summary>
    /// <param name="mapper">要配置的 BsonMapper 实例</param>
    public static void ConfigureNestedTypes(BsonMapper mapper)
    {
        // 显式注册 EmergencyStopButtonConfig 以避免 LiteDB 序列化 record 类型时的"Invalid handle"错误
        // 这是因为 record 类型可能包含编译器生成的方法（如 Equals/GetHashCode/ToString），
        // LiteDB 在自动发现类型时可能错误地尝试序列化这些方法引用
        //
        // 通过简单注册实体，让 LiteDB 知道这是一个需要序列化的类型
        // IncludeNonPublic = true 应该已经在 CreateConfiguredMapper 中设置，能够访问 record 的 init 属性
        mapper.Entity<EmergencyStopButtonConfig>();
    }

    /// <summary>
    /// 创建一个配置了枚举字符串序列化和 Id 映射的新 BsonMapper
    /// </summary>
    /// <returns>配置好的 BsonMapper 实例</returns>
    public static BsonMapper CreateConfiguredMapper()
    {
        var mapper = new BsonMapper();
        
        // 注意：IncludeNonPublic 在 .NET 9 + LiteDB 5.0.21 中可能导致序列化错误
        // RoutePlan 现在使用 internal set 属性，Configuration.Persistence 程序集通过 InternalsVisibleTo 访问
        // mapper.IncludeNonPublic = true;
        
        // 配置序列化行为
        mapper.SerializeNullValues = false;  // 不序列化 null 值
        mapper.TrimWhitespace = false;  // 不修剪空白字符
        
        ConfigureEnumAsString(mapper);
        ConfigureEntityIds(mapper);
        ConfigureNestedTypes(mapper);
        return mapper;
    }
}
