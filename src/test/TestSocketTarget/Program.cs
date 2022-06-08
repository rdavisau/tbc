using System.IO.Abstractions;
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
using Tbc.Target.Config;
using Tbc.Target.Implementation;
using Tbc.Target.Requests;
using TestSocketTarget;

var rm = new MyReloadManager();
var listener = new TcpListener(IPAddress.Any, 0);
var handler = new AssemblyLoaderService(TargetConfiguration.Default(applicationIdentifier:"my-app"), rm, Console.WriteLine);
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
    new RemoteClient { Address = "localhost", Port = ep.Port },
    new FileSystem());

host.ClientChannelState.Subscribe(x => Console.WriteLine(x));

Console.WriteLine("Waiting for connection");
await host.WaitForConnection().ConfigureAwait(false);

Console.WriteLine("Got connection");
var targetHello = await host.Hello(new HostHello { SharedHostFilePath = Path.GetTempFileName() });

var existingReferences = new List<AssemblyReference>();

if (targetHello.CanAccessSharedHostFile)
{
    var files = Directory.GetFiles(targetHello.RootAssemblyPath, "*.dll");
    foreach (var f in files)
    {
        var reference = new AssemblyReference
        {
            AssemblyLocation = f,
            ModificationTime = new FileInfo(f).LastWriteTime,
            PeBytes = await File.ReadAllBytesAsync(f)
        };

        existingReferences.Add(reference);
    }
}

var lateReferences = new List<AssemblyReference>();
Task.Run(async () =>
{
    await foreach (var lateReference in await host.AssemblyReferences(existingReferences))
    {
        lateReferences.Add(lateReference);
        var a = 1;
    }
});

await Task.Delay(TimeSpan.FromSeconds(6));

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
