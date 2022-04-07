using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Host.Extensions;
using Tbc.Protocol;
using CachedAssemblyState = Tbc.Protocol.CachedAssemblyState;

namespace Tbc.Host.Components.TargetClient.GrpcCore
{
    public class GrpcCoreTargetClient : ComponentBase<GrpcCoreTargetClient>, ITargetClient
    {
        public IRemoteClientDefinition ClientDefinition { get; }
        
        private readonly Subject<ChannelState> _channelStateSub = new();
        private CanonicalChannelState ToCanonicalChannelState(ChannelState state)
            => Enum.Parse<CanonicalChannelState>(state.ToString()); // CanonicalChannelState is a clone of ChannelState

        public IObservable<CanonicalChannelState> ClientChannelState
            => _channelStateSub
               .Select(ToCanonicalChannelState)
               .AsObservable();

        private AssemblyLoader.AssemblyLoaderClient Loader { get; set; }
        private Channel Channel { get; set; }

        public GrpcCoreTargetClient(IRemoteClientDefinition client, ILogger<GrpcCoreTargetClient> logger) : base(logger)
        {
            ClientDefinition = client;
        }

        public async Task WaitForConnection()
        {
            var channel = new Channel(
                ClientDefinition.Address, ClientDefinition.Port,
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
                => Logger.LogInformation("Client {Client} channel state is now '{ChannelState}'", ClientDefinition, s);
            
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

        public async Task<IAsyncEnumerable<Core.Models.AssemblyReference>> AssemblyReferences()
        {
            if (Channel?.State != ChannelState.Ready)
                throw new Exception($"Channel state is '{Channel?.State}' but needed '{ChannelState.Ready}'");
            
            while (true)
            {
                try
                {
                    Loader ??= new AssemblyLoader.AssemblyLoaderClient(Channel);

                    return Loader.SynchronizeDependencies(new CachedAssemblyState()).ResponseStream
                       .ReadAllAsync(x => x.ToCanonical());
                }
                catch (Exception ex)
                {
                    await Task.Delay(TimeSpan.FromSeconds(.33));
                }
            }
        }
        
        public async Task<IAsyncEnumerable<Core.Models.ExecuteCommandRequest>> CommandRequests()
        {
            if (Channel?.State != ChannelState.Ready)
                throw new Exception($"Channel state is '{Channel?.State}' but needed '{ChannelState.Ready}'");
            
            while (true)
            {
                try
                {
                    return Loader.RequestCommand(new Unit()).ResponseStream.ReadAllAsync(x => x.ToCanonical());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    await Task.Delay(TimeSpan.FromSeconds(.33));
                }
            }
        }
        public override string ToString()
        {
            return $"{ClientDefinition.Address}:{ClientDefinition.Port} (Channel State: {Channel?.State})";
        }

        public void Dispose()
        {
            
        }

        public Task WaitForTerminalState() =>
            ClientChannelState
                .TakeUntil(x => x == CanonicalChannelState.Shutdown
                             || x == CanonicalChannelState.TransientFailure
                             || x == CanonicalChannelState.Idle)
                .ToTask();

        public Task<Core.Models.Outcome> ExecAsync(Tbc.Core.Models.ExecuteCommandRequest req)
            => Loader.ExecAsync(req.ToCore()).ResponseAsync.ContinueWith(t => t.Result.ToCanonical());

        public Task<Core.Models.Outcome> LoadAssemblyAsync(Tbc.Core.Models.LoadDynamicAssemblyRequest req)
            => Loader.LoadAssemblyAsync(req.ToCore()).ResponseAsync.ContinueWith(t => t.Result.ToCanonical());
    }
}
