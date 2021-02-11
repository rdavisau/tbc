namespace Tbc.Host.Components.FileEnvironment.Models
{
    public interface IClient
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }
}