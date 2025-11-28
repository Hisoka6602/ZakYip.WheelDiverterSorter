using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;

/// <summary>
/// LiteDB BsonMapper 配置助手类
/// </summary>
/// <remarks>
/// 配置 LiteDB 的序列化行为，确保枚举以字符串形式存储而不是整数
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
    /// 创建一个配置了枚举字符串序列化的新 BsonMapper
    /// </summary>
    /// <returns>配置好的 BsonMapper 实例</returns>
    public static BsonMapper CreateConfiguredMapper()
    {
        var mapper = new BsonMapper();
        ConfigureEnumAsString(mapper);
        return mapper;
    }
}
