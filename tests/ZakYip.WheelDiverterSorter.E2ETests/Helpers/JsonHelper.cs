using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZakYip.WheelDiverterSorter.E2ETests.Helpers;

/// <summary>
/// JSON序列化辅助类，提供统一的JSON序列化选项
/// </summary>
/// <remarks>
/// 确保测试中的JSON反序列化行为与生产环境一致，特别是枚举的序列化方式
/// </remarks>
public static class JsonHelper
{
    /// <summary>
    /// 获取默认的JSON序列化选项（与Host层配置保持一致）
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            // 配置枚举序列化为字符串，与Host层保持一致
            // allowIntegerValues: true 允许反序列化时接受数字（向后兼容）
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: true));

        return options;
    }

    /// <summary>
    /// 从HttpContent中反序列化JSON，使用与生产环境一致的选项（包括枚举转换）
    /// </summary>
    public static async Task<T?> ReadJsonAsync<T>(this HttpContent content, CancellationToken cancellationToken = default)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, DefaultOptions, cancellationToken);
    }
}
