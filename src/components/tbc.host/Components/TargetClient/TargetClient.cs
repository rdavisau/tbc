using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Protocol;

namespace Tbc.Host.Components.TargetClient
{
    public class TargetClient : ComponentBase<TargetClient>
    {
        private readonly IClient _client;
        
        private readonly Subject<ChannelState> _channelStateSub = new Subject<ChannelState>();
        public IObservable<ChannelState> ClientChannelState => _channelStateSub.AsObservable();

        public TargetClient(IClient client, ILogger<TargetClient> logger) : base(logger)
        {
            _client = client;
        }

        public async Task WaitForConnection()
        {
            var channel = new Channel(
                _client.Address, _client.Port,
                ChannelCredentials.Insecure,
                new []
                {
                    new ChannelOption(ChannelOptions.MaxReceiveMessageLength, -1),
                    new ChannelOption(ChannelOptions.MaxSendMessageLength, -1)
                });

            await channel.ConnectAsync();

            Task.Run(async () => await TrackChannelState(channel));

            Channel = channel;
        }

        private async Task TrackChannelState(Channel channel)
        {
            void LogState(ChannelState s)
                => Logger.LogInformation("Client {Client} channel state is now '{ChannelState}'", Client, s);
            
            var state = channel.State;
            LogState(state);
            
            while (state != ChannelState.Shutdown && state != ChannelState.TransientFailure)
            {
                await channel.TryWaitForStateChangedAsync(channel.State);
                
                state = channel.State;
                LogState(state);
                
                _channelStateSub.OnNext(state);
            }
        }

        public async Task<IAsyncStreamReader<AssemblyReference>> AssemblyReferences()
        {
            if (Channel?.State != ChannelState.Ready)
                throw new Exception($"Channel state is '{Channel?.State}' but needed '{ChannelState.Ready}'");
            
            while (true)
            {
                try
                {
                    Loader ??= new AssemblyLoader.AssemblyLoaderClient(Channel);
                    
                    return Loader.SynchronizeDependencies(new CachedAssemblyState()).ResponseStream;
                }
                catch (Exception ex)
                {
                    await Task.Delay(TimeSpan.FromSeconds(.33));
                }
            }
        }
        
        public async Task<IAsyncStreamReader<ExecuteCommandRequest>> CommandRequests()
        {
            if (Channel?.State != ChannelState.Ready)
                throw new Exception($"Channel state is '{Channel?.State}' but needed '{ChannelState.Ready}'");
            
            while (true)
            {
                try
                {
                    return Loader.RequestCommand(new Unit()).ResponseStream;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    await Task.Delay(TimeSpan.FromSeconds(.33));
                }
            }
        }

        public AssemblyLoader.AssemblyLoaderClient Loader { get; set; }

        public Channel Channel { get; set; }

        public override string ToString()
        {
            return $"{_client.Address}:{_client.Port} (Channel State: {Channel?.State})";
        }

        public IClient Client => _client;

        public void Dispose()
        {
            
        }

        public Task WaitForTerminalState() =>
            ClientChannelState
                .TakeUntil(x => x == ChannelState.Shutdown || x == ChannelState.TransientFailure || x == ChannelState.Idle)
                .ToTask();
    }
}