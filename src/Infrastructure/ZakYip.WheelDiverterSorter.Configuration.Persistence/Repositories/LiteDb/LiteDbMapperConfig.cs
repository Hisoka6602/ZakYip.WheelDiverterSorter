using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
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
        
        // WheelBindingsConfig removed - wheel binding now handled via topology + vendor config association
    }

    /// <summary>
    /// 创建一个配置了枚举字符串序列化和 Id 映射的新 BsonMapper
    /// </summary>
    /// <returns>配置好的 BsonMapper 实例</returns>
    public static BsonMapper CreateConfiguredMapper()
    {
        var mapper = new BsonMapper();
        
        // 启用非公共成员访问，以支持具有私有 setter 的实体（如 RoutePlan）
        // Enable non-public member access to support entities with private setters (e.g., RoutePlan)
        mapper.IncludeNonPublic = true;
        
        ConfigureEnumAsString(mapper);
        ConfigureEntityIds(mapper);
        return mapper;
    }
}
