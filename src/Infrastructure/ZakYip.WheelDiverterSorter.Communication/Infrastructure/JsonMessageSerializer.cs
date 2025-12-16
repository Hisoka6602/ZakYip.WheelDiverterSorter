using System.Buffers;
using System.Text;
using System.Text.Json;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// JSON消息序列化器（使用 Span<T> 和 ArrayPool 优化内存分配）
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

        // 使用 JsonSerializer.SerializeToUtf8Bytes 避免中间字符串分配
        return JsonSerializer.SerializeToUtf8Bytes(obj, _options);
    }

    public T? Deserialize<T>(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return default;
        }

        // 使用 ReadOnlySpan<byte> 避免字符串分配
        return JsonSerializer.Deserialize<T>(new ReadOnlySpan<byte>(data), _options);
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

    /// <summary>
    /// 高性能序列化到 IMemoryOwner（调用方负责释放）
    /// </summary>
    /// <returns>可释放的内存所有者，使用完毕后必须调用 Dispose()</returns>
    public RentedBuffer SerializeToRentedBuffer<T>(T obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        // 使用 Utf8JsonWriter 直接写入缓冲区
        var bufferWriter = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(bufferWriter))
        {
            JsonSerializer.Serialize(writer, obj, _options);
        }

        var writtenSpan = bufferWriter.WrittenSpan;
        var buffer = BufferPoolManager.Shared.RentSmallBuffer(writtenSpan.Length);
        
        writtenSpan.CopyTo(buffer);
        return new RentedBuffer(buffer, writtenSpan.Length);
    }

    /// <summary>
    /// 高性能反序列化从 ReadOnlySpan<byte>
    /// </summary>
    public T? DeserializeFromSpan<T>(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(data, _options);
    }
}

/// <summary>
/// 租用的缓冲区包装器，确保使用后正确归还
/// </summary>
public readonly struct RentedBuffer : IDisposable
{
    private readonly byte[] _buffer;
    
    public int BytesWritten { get; }
    
    public ReadOnlySpan<byte> Data => new ReadOnlySpan<byte>(_buffer, 0, BytesWritten);

    internal RentedBuffer(byte[] buffer, int bytesWritten)
    {
        _buffer = buffer;
        BytesWritten = bytesWritten;
    }

    public void Dispose()
    {
        if (_buffer != null)
        {
            BufferPoolManager.Shared.ReturnSmallBuffer(_buffer);
        }
    }
}

