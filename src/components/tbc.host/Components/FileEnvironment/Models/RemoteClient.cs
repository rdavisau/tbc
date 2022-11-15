namespace Tbc.Host.Components.FileEnvironment.Models
{
    public record RemoteClient : IRemoteClientDefinition
    {
        public required string Address { get; init; }
        public required int Port { get; set; }

        public override string ToString()
            => $"{Address}:{Port}";
    }
}
