namespace Tbc.Host.Components.FileEnvironment.Models
{
    public interface IRemoteClientDefinition
    {
        public string Address { get; }
        public int Port { get; }

        public string HttpAddress => $"http://{Address}:{Port}";
    }
}
