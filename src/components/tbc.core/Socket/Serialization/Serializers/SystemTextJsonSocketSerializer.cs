using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Tbc.Core.Socket.Abstractions;

namespace Tbc.Core.Socket.Serialization.Serializers;

public class SystemTextJsonSocketSerializer : ISocketSerializer
{
    public byte[] Serialize(object data)
    {
        using var ms = new MemoryStream();
        using var gz = new GZipStream(ms, CompressionLevel.Optimal);
        using var writer = new Utf8JsonWriter(gz);

        JsonSerializer.Serialize(writer, data, _serializerOptions);
        writer.Flush();
        gz.Flush();

        ms.Seek(0, SeekOrigin.Begin);
        return ms.ToArray();
    }

    public object Deserialize(Type type, byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var gz = new GZipStream(ms, CompressionMode.Decompress);

        var ret = JsonSerializer.Deserialize(gz, type, _serializerOptions);

        return ret!;
    }

    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
}
