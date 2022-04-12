using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tbc.Core.Apis;
using Tbc.Core.Models;
using Tbc.Core.Socket;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Host.Components.TargetClient;
using Tbc.Target;
using Tbc.Target.Implementation;
using Tbc.Target.Requests;
using TestSocketTarget;

var rm = new MyReloadManager();
var listener = new TcpListener(IPAddress.Any, 0);
var handler = new AssemblyLoaderService(rm, Console.WriteLine);
listener.Start();

Task.Run(async () =>
{
    while (true)
    {
        try
        {
            var connection = await listener.AcceptTcpClientAsync();
            var socketServer = new SocketServer<ITbcProtocol>(connection, handler, "client", Console.WriteLine);
            await socketServer.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"socket loop iteration faulted: {ex.ToString()}");
            await Task.Delay(TimeSpan.FromSeconds(.5));
        }
    }
});

var ep = ((IPEndPoint)listener.LocalEndpoint);

var serviceCollection = new ServiceCollection();
serviceCollection.AddLogging(configure => configure.AddConsole());
var serviceProvider = serviceCollection.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<SocketTargetClient>>();

var host = new SocketTargetClient(
    logger,
    new RemoteClient { Address = "localhost", Port = ep.Port });

host.ClientChannelState.Subscribe(x => Console.WriteLine(x));

Console.WriteLine("Waiting for connection");
await host.WaitForConnection().ConfigureAwait(false);

Console.WriteLine("Got connection");

Task.Run(async () => host.AssemblyReferences());

Console.ReadLine();


namespace TestSocketTarget
{
    public class MyReloadManager : ReloadManagerBase
    {
        public override Task<Outcome> ProcessNewAssembly(ProcessNewAssemblyRequest req)
        {
            Console.WriteLine(req);
            return Task.FromResult(new Outcome());
        }

        public override Task<Outcome> ExecuteCommand(CommandRequest req)
        {
            Console.WriteLine(req);
            return Task.FromResult(new Outcome());
        }
    }
}
