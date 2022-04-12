using System;
using MessagePack;
using Tbc.Core.Socket.Abstractions;

namespace Tbc.Core.Socket.Serialization.Serializers;

public class MessagePackSocketSerializer : ISocketSerializer
{
    public byte[] Serialize(object data)
        => MessagePackSerializer.Serialize(data.GetType(), data, _serializationOptions);

    public object Deserialize(Type type, byte[] data)
        => MessagePackSerializer.Deserialize(type, data, _serializationOptions);

    private readonly MessagePackSerializerOptions _serializationOptions =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
}