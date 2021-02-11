namespace Tbc.Target.Config
{
    public class TargetConfiguration
    {
        public int ListenPort { get; set; }

        public static TargetConfiguration Default(int port = 50123)
            => new TargetConfiguration { ListenPort = port };
    }
}