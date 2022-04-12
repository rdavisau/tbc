using System;
using System.Collections.Generic;
using MessagePack;

namespace Tbc.Core.Models;

public record ConnectRequest
{
    public HostInfo HostInfo { get; set; } = default!;
}

public record HostInfo
{
    public string Address { get; set; }  = default!;
    public int Port { get; set; }

    public string HttpAddress => $"http://{Address}:{Port}";
}

public record ConnectResponse
{
    public string AssemblyName { get; set; } = default!;
}

public record HeartbeatRequest { }

public interface ISocketMessage
{
    SocketMessageKind Kind { get; }
    string RequestIdentifier { get; set; }
    object Payload { get; }
}

[MessagePackObject]
public record SocketRequest<T> : ISocketMessage
{
    [Key(0)]
    public string RequestIdentifier { get; set; } = default!;

    [Key(1)]
    public T Payload { get; set; } = default!;

    SocketMessageKind ISocketMessage.Kind => SocketMessageKind.Request;
    object ISocketMessage.Payload => Payload;
}

[MessagePackObject]
public record SocketResponse<T> : ISocketMessage
{
    [Key(0)]
    public string RequestIdentifier { get; set; } = default!;

    [Key(1)]
    public SocketRequestOutcome Outcome { get; set; }

    [Key(2)]
    public T Data { get; set; } = default!;

    [Key(3)]
    public string? ErrorData { get; set; }

    SocketMessageKind ISocketMessage.Kind => SocketMessageKind.Response;
    object ISocketMessage.Payload => Data;

    public SocketResponse() { }

    public SocketResponse(string requestIdentifier, T data)
    {
        RequestIdentifier = requestIdentifier;
        Data = data;
        Outcome = SocketRequestOutcome.Success;
    }
}

public enum SocketRequestOutcome
{
    Success,
    ProtocolNotRecognised,
    RequestNotHandled,
    Error
}

public enum SocketMessageKind
{
    Unset,
    Request,
    Response
}

public record ReceiveResult
{
    public ReceiveResultOutcome Outcome { get; set; }
    public SocketMessageKind Kind { get; set; }
    public ISocketMessage? Message { get; set; }
    public ISocketMessage? Response { get; set; }

    public Exception? Exception { get; set; } = default!;
}

public enum ReceiveResultOutcome
{
    Success,
    ProtocolNotRecognised,
    RequestNotHandled,
    WaywardMessage,
    Error,
    Disconnect
}

[MessagePackObject]
public record LoadDynamicAssemblyRequest
{
    [Key(0)]
    public byte[] PeBytes { get; set; } = default!;

    [Key(1)]
    public byte[]? PdbBytes { get; set; }

    [Key(2)]
    public string AssemblyName { get; set; } = default!;

    [Key(3)]
    public string PrimaryTypeName { get; set; } = default!;
}

[MessagePackObject]
public record Outcome
{
    [Key(0)]
    public bool Success { get; set; }

    [Key(1)]
    public List<OutcomeMessage> Messages { get; set; } = new();
}

[MessagePackObject]
public record OutcomeMessage
{
    [Key(0)]
    public string Message { get; set; } = default!;
}

[MessagePackObject]
public record ExecuteCommandRequest
{
    [Key(0)]
    public string Command { get; set; } = default!;

    [Key(1)]
    public List<string> Args { get; set; } = new();
}

[MessagePackObject]
public record CachedAssemblyState
{
    [Key(0)]
    public List<CachedAssembly> CachedAssemblies { get; set; } = new();
}

[MessagePackObject]
public record CachedAssembly
{
    [Key(0)]
    public string AssemblyName { get; set; } = default!;
    [Key(1)]
    public DateTimeOffset ModificationTime { get; set; }
}

[MessagePackObject]
public record AssemblyReference
{
    [Key(0)]
    public string AssemblyName { get; set; } = default!;
    [Key(1)]
    public string AssemblyLocation { get; set; } = default!;
    [Key(2)]
    public DateTimeOffset ModificationTime { get; set; } = default!;
    [Key(3)]
    public byte[] PeBytes { get; set; } = default!;
}

[MessagePackObject]
public record ManyAssemblyReferences
{
    [Key(0)] public List<AssemblyReference> AssemblyReferences { get; set; } = new();
}
