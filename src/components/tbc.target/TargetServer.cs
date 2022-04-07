using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Grpc.Core;
using Tbc.Protocol;
using Tbc.Target.Config;
using Tbc.Target.Interfaces;

namespace Tbc.Target
{
    public class TargetServer
    {
        public TargetConfiguration Configuration { get; }

        public TargetServer() : this(TargetConfiguration.Default())
        {
            
        }

        public TargetServer(TargetConfiguration configuration)
        {
            Configuration = configuration;
        }

        public async Task Run(IReloadManager reloadManager, Action<string> log = null)
        {
            var server = new Server(new []
            {
                new ChannelOption(ChannelOptions.MaxReceiveMessageLength, 838860800),
                new ChannelOption(ChannelOptions.MaxSendMessageLength, 838860800)
            })
            {
                Services = { Protocol.AssemblyLoader.BindService(new AssemblyLoaderService(
                    reloadManager, 
                    log ?? (s => Debug.WriteLine(s))
                )) },
                Ports = { new ServerPort("0.0.0.0", Configuration.ListenPort, ServerCredentials.Insecure) }
            };
            
            server.Start();
        }
    }
}
