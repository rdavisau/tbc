namespace Tbc.Core.Socket.Abstractions;

public interface ISendToRemote
{
    IRemoteEndpoint Remote { get; set; }
}