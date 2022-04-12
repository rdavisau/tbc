using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Tbc.Core.Apis;
using Tbc.Core.Socket;
using Tbc.Target.Config;
using Tbc.Target.Implementation;
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
            log ??= Console.WriteLine;

            var listener = new TcpListener(IPAddress.Any, Configuration.ListenPort);
            var handler = new AssemblyLoaderService(reloadManager, log);

            listener.Start();

            Task.Run(async () =>
            {
                while (true)
                {
                    var connection = await listener.AcceptTcpClientAsync();

                    try
                    {
                        var socketServer = new SocketServer<ITbcProtocol>(connection, handler, "client", log);
                        await socketServer.Run();
                    }
                    catch (Exception ex)
                    {
                        log($"socket loop iteration faulted: {ex.ToString()}");
                    }
                }
            })
           .ContinueWith(t =>
            {
                log($"socket loop faulted: {t.Exception}");
            });
        }
    }
}
