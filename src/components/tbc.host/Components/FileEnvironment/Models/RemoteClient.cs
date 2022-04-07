namespace Tbc.Host.Components.FileEnvironment.Models
{
    public class RemoteClient : IRemoteClientDefinition
    {
        public string Address { get; set; }
        public int Port { get; set; }

        public override string ToString()
            => $"{Address}:{Port}";
    }
}