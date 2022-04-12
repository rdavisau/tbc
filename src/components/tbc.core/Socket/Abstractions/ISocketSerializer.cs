using System;

namespace Tbc.Core.Socket.Abstractions;

public interface ISocketSerializer
{
    byte[] Serialize(object data);
    object Deserialize(Type type, byte[] data);
}