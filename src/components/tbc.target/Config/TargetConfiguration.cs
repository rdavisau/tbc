using System;

namespace Tbc.Target.Config
{
    public class TargetConfiguration
    {
        public string? ApplicationIdentifier { get; set; }
        public int ListenPort { get; set; }
        public bool UseDependencyCache { get; set; }
        public bool UseSharedFilesystemDependencyResolutionIfPossible { get; set; }

        public static TargetConfiguration Default(
            int port = 50123,
            string? applicationIdentifier = default,
            bool useDependencyCache = true,
            bool useSharedFilesystemDependencyResolutionIfPossible = true)
            => new()
            {
                ListenPort = port,
                ApplicationIdentifier = applicationIdentifier,
                UseDependencyCache = useDependencyCache,
                UseSharedFilesystemDependencyResolutionIfPossible = useSharedFilesystemDependencyResolutionIfPossible,
            };
    }
}
