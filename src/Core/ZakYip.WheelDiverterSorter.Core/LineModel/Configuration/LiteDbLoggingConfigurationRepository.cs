using LiteDB;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 基于LiteDB的日志配置仓储实现
/// </summary>
public class LiteDbLoggingConfigurationRepository : ILoggingConfigurationRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<LoggingConfiguration> _collection;
    private readonly ISystemClock _systemClock;
    private const string CollectionName = "LoggingConfiguration";
    private const string LoggingConfigName = "logging";

    /// <summary>
    /// 初始化LiteDB日志配置仓储
    /// </summary>
    /// <param name="databasePath">LiteDB数据库文件路径</param>
    /// <param name="systemClock">系统时钟</param>
    public LiteDbLoggingConfigurationRepository(string databasePath, ISystemClock systemClock)
    {
        // 使用Shared模式允许多个仓储实例共享同一个数据库文件
        var connectionString = $"Filename={databasePath};Connection=shared";
        _database = new LiteDatabase(connectionString, LiteDbMapperConfig.CreateConfiguredMapper());
        _collection = _database.GetCollection<LoggingConfiguration>(CollectionName);
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));

        // 为ConfigName字段创建唯一索引
        _collection.EnsureIndex(x => x.ConfigName, unique: true);
    }

    /// <summary>
    /// 获取日志配置
    /// </summary>
    /// <returns>日志配置，如不存在则返回默认配置</returns>
    public LoggingConfiguration Get()
    {
        var config = _collection
            .Query()
            .Where(x => x.ConfigName == LoggingConfigName)
            .FirstOrDefault();

        if (config == null)
        {
            // 如果不存在，初始化默认配置并返回
            InitializeDefault();
            config = _collection
                .Query()
                .Where(x => x.ConfigName == LoggingConfigName)
                .FirstOrDefault();
        }

        return config ?? LoggingConfiguration.GetDefault();
    }

    /// <summary>
    /// 更新日志配置
    /// </summary>
    /// <param name="configuration">日志配置</param>
    public void Update(LoggingConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // 验证配置
        var (isValid, errorMessage) = configuration.Validate();
        if (!isValid)
        {
            throw new ArgumentException(errorMessage, nameof(configuration));
        }

        // 确保ConfigName为logging
        configuration.ConfigName = LoggingConfigName;
        configuration.UpdatedAt = _systemClock.LocalNow;

        // 查找现有配置
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == LoggingConfigName)
            .FirstOrDefault();

        if (existing != null)
        {
            // 更新现有配置，保留原有ID和创建时间，增加版本号
            configuration.Id = existing.Id;
            configuration.CreatedAt = existing.CreatedAt;
            configuration.Version = existing.Version + 1;
            _collection.Update(configuration);
        }
        else
        {
            // 插入新配置
            configuration.Version = 1;
            configuration.CreatedAt = _systemClock.LocalNow;
            _collection.Insert(configuration);
        }
    }

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    public void InitializeDefault(DateTime? currentTime = null)
    {
        // 检查是否已有配置
        var existing = _collection
            .Query()
            .Where(x => x.ConfigName == LoggingConfigName)
            .FirstOrDefault();

        if (existing == null)
        {
            var defaultConfig = LoggingConfiguration.GetDefault();
            // 如果提供了当前时间，则使用；否则使用系统时钟的本地时间
            var now = currentTime ?? _systemClock.LocalNow;
            defaultConfig.CreatedAt = now;
            defaultConfig.UpdatedAt = now;
            _collection.Insert(defaultConfig);
        }
    }

    /// <summary>
    /// 释放数据库资源
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }
}
