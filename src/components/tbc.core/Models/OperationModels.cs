using System;
using System.Collections.Generic;

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

public record SocketRequest<T> : ISocketMessage
{
    public string RequestIdentifier { get; set; } = default!;

    public T Payload { get; init; } = default!;

    SocketMessageKind ISocketMessage.Kind => SocketMessageKind.Request;
    object ISocketMessage.Payload => Payload!;
}

public record SocketResponse<T> : ISocketMessage
{
    public string RequestIdentifier { get; set; } = default!;

    public SocketRequestOutcome Outcome { get; set; }

    public T Data { get; set; } = default!;

    public string? ErrorData { get; set; }

    SocketMessageKind ISocketMessage.Kind => SocketMessageKind.Response;
    object ISocketMessage.Payload => Data!;

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

public record LoadDynamicAssemblyRequest
{
    public byte[] PeBytes { get; set; } = default!;

    public byte[]? PdbBytes { get; set; }

    public string AssemblyName { get; set; } = default!;

    public string? PrimaryTypeName { get; set; } = default!;
}

public record Outcome
{
    public bool Success { get; set; }

    public List<OutcomeMessage> Messages { get; set; } = new();
}

public record OutcomeMessage
{
    public string Message { get; set; } = default!;
}

public record ExecuteCommandRequest
{
    public string Command { get; set; } = default!;

    public List<string> Args { get; set; } = new();
}

public record CachedAssemblyState
{
    public List<CachedAssembly> CachedAssemblies { get; set; } = new();
}

public record CachedAssembly
{
    public CachedAssembly() {}

    public CachedAssembly(AssemblyReference assemblyReference)
    {
        AssemblyName = assemblyReference.AssemblyName;
        AssemblyLocation = assemblyReference.AssemblyLocation;
        ModificationTime = assemblyReference.ModificationTime;
    }

    public string? AssemblyName { get; set; } = default!;
    public string AssemblyLocation { get; set; } = default!;
    public DateTimeOffset ModificationTime { get; set; }
}

public record AssemblyReference
{
    public string AssemblyName { get; set; } = default!;
    public string AssemblyLocation { get; set; } = default!;
    public DateTimeOffset ModificationTime { get; set; } = default!;
    public byte[]? PeBytes { get; set; } = default!;
}

public record ManyAssemblyReferences
{
    public List<AssemblyReference> AssemblyReferences { get; set; } = new();
}

public record HostHello
{
    public string SharedHostFilePath { get; set; } = default!;
}

public record TargetHello
{
    public string? ApplicationIdentifier { get; set; } = default!;

    public bool CanAccessSharedHostFile { get; set; }

    public bool UseSharedFilesystemDependencyResolution { get; set; }

    public string RootAssemblyPath { get; set; } = default!;

    public bool UseDependencyCache { get; set; }
}
