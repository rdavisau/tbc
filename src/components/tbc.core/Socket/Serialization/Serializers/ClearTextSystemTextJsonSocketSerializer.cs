using System;
using System.Text;
using System.Text.Json;
using Tbc.Core.Socket.Abstractions;

namespace Tbc.Core.Socket.Serialization.Serializers;

public class ClearTextSystemTextJsonSocketSerializer : ISocketSerializer
{
    public byte[] Serialize(object data)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, _serializerOptions));
        return bytes;
    }

    public object Deserialize(Type type, byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        var ret = JsonSerializer.Deserialize(json, type, _serializerOptions);

        return ret;
    }

    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
}
