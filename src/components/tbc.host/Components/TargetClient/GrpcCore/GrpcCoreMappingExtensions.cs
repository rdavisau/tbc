using System;
using System.Linq;
using Google.Protobuf;
using Tbc.Core.Models;
using AssemblyReference = Tbc.Protocol.AssemblyReference;
using ExecuteCommandRequest = Tbc.Protocol.ExecuteCommandRequest;
using LoadDynamicAssemblyRequest = Tbc.Protocol.LoadDynamicAssemblyRequest;
using Outcome = Tbc.Protocol.Outcome;

namespace Tbc.Host.Components.TargetClient.GrpcCore;

public static class GrpcCoreMappingExtensions
{
    public static Tbc.Core.Models.Outcome ToCanonical(this Outcome o)
        => new() {
            Success = o.Success,
            Messages = o.Messages.Select(x => new OutcomeMessage { Message = x.Message_ }).ToList()
        };

    public static Tbc.Core.Models.ExecuteCommandRequest ToCanonical(this ExecuteCommandRequest x)
        => new() { Command = x.Command, Args = x.Args.ToList() };

    public static Tbc.Core.Models.AssemblyReference ToCanonical(this AssemblyReference x)
        => new()
        {
            AssemblyName = x.AssemblyName,
            ModificationTime = DateTimeOffset.FromUnixTimeSeconds((long)x.ModificationTime),
            AssemblyLocation = x.AssemblyLocation,
            PeBytes = x.PeBytes.ToByteArray()
        };

    public static LoadDynamicAssemblyRequest ToCore(this Tbc.Core.Models.LoadDynamicAssemblyRequest req)
        => new()
        {
            AssemblyName = req.AssemblyName,
            PeBytes = ByteString.CopyFrom(req.PeBytes),
            PdbBytes = ByteString.CopyFrom(req.PdbBytes),
            PrimaryTypeName = req.PrimaryTypeName
        };

    public static ExecuteCommandRequest ToCore(this Tbc.Core.Models.ExecuteCommandRequest req)
        => new()
        {
            Command = req.Command,
            Args = { req.Args }
        };
}
