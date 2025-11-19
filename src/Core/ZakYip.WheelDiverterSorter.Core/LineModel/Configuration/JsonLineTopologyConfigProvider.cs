using System.Text.Json;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 基于JSON文件的线体拓扑配置提供者
/// </summary>
/// <remarks>
/// 从JSON文件读取拓扑配置，适用于仿真环境和测试场景
/// </remarks>
public class JsonLineTopologyConfigProvider : ILineTopologyConfigProvider
{
    private readonly string _configFilePath;
    private LineTopologyConfig? _cachedConfig;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configFilePath">JSON配置文件路径</param>
    public JsonLineTopologyConfigProvider(string configFilePath)
    {
        _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <inheritdoc />
    public async Task<LineTopologyConfig> GetTopologyAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            return await LoadConfigFromFileAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _cachedConfig = null;
            _cachedConfig = await LoadConfigFromFileAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<LineTopologyConfig> LoadConfigFromFileAsync()
    {
        if (!File.Exists(_configFilePath))
        {
            throw new FileNotFoundException($"拓扑配置文件不存在: {_configFilePath}");
        }

        var json = await File.ReadAllTextAsync(_configFilePath);
        var config = JsonSerializer.Deserialize<LineTopologyConfig>(json, _jsonOptions);

        if (config == null)
        {
            throw new InvalidOperationException($"无法解析拓扑配置文件: {_configFilePath}");
        }

        _cachedConfig = config;
        return config;
    }
}
