using System.Text;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// JSON消息序列化器
/// </summary>
public class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonMessageSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public byte[] Serialize<T>(T obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var json = JsonSerializer.Serialize(obj, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    public T? Deserialize<T>(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return default;
        }

        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    public string SerializeToString<T>(T obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        return JsonSerializer.Serialize(obj, _options);
    }

    public T? DeserializeFromString<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, _options);
    }
}
