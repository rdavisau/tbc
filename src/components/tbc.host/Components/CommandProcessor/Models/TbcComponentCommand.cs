namespace Tbc.Host.Components.CommandProcessor.Models
{
    public class TbcComponentCommand
    {
        public required string ComponentIdentifier { get; init; }
        public required TbcCommand Command { get; init; }
    }
}
