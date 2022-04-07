using System;
using System.Collections.Generic;

namespace Tbc.Core.Models;

public record ConnectRequest
{
    public HostInfo HostInfo { get; set; }
}

public record ConnectResponse
{
    public string AssemblyName { get; set; }
}

public record LoadDynamicAssemblyRequest
{
    public byte[] PeBytes { get; set; }
    public byte[] PdbBytes { get; set; }
    public string AssemblyName { get; set; }
    public string PrimaryTypeName { get; set; }
}

public record Outcome
{
    public bool Success { get; set; }
    public List<OutcomeMessage> Messages { get; set; } = new();
}

public record OutcomeMessage
{
    public string Message { get; set; }
}

public record HostInfo
{
    public string Address { get; set; }
    public int Port { get; set; }
}

public record ExecuteCommandRequest
{
    public string Command { get; set; }
    public List<string> Args { get; set; } = new();
}

public record CachedAssemblyState
{
    public List<CachedAssembly> CachedAssemblies { get; set; } = new();
}

public record CachedAssembly
{
    public string AssemblyName { get; set; } = default!;
    public DateTimeOffset ModificationTime { get; set; }
}

public record AssemblyReference
{
    public string AssemblyName { get; set; } = default!;
    public string AssemblyLocation { get; set; } = default!;
    public DateTimeOffset ModificationTime { get; set; } = default!;
    public byte[] PeBytes { get; set; } = default!;
}
