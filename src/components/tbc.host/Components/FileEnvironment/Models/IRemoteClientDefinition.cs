namespace Tbc.Host.Components.FileEnvironment.Models
{
    public interface IRemoteClientDefinition
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }
}
