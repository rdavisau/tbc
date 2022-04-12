using System;
using System.Threading.Tasks;

namespace Tbc.Core.Socket.Models;

public record SocketHandlerOperation(string Name, Type RequestType, Type ResponseType, Func<object, Task<object>> Handler);