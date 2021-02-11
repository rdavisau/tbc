using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Tbc.Protocol;
using Tbc.Target.Interfaces;
using Tbc.Target.Requests;

namespace Tbc.Target
{
    public class AssemblyLoaderService : AssemblyLoader.AssemblyLoaderBase
    {
        private IReloadManager _reloadManager;
        private readonly Action<string> _log;

        public AssemblyLoaderService(IReloadManager reloadManager, Action<string> log)
        {
            _reloadManager = reloadManager;
            _log = log;

            OnReloadManager(reloadManager);
        }

        private void OnReloadManager(IReloadManager reloadManager)
        {
            _reloadManager = reloadManager;

            if (reloadManager is INotifyReplacement inr)
                inr.NotifyReplacement = rm =>
                {
                    inr.NotifyReplacement = null;

                    _log($"Replacing reload manager: {rm}");
                    
                    OnReloadManager(rm);
                };

            if (reloadManager is IRequestHostCommand irehc)
                irehc.RequestHostCommand += req 
                    => RequestHostCommandImpl?.Invoke(req);
        }

        public override async Task<Outcome> LoadAssembly(LoadDynamicAssemblyRequest request, ServerCallContext context)
        {
            try
            {
                var peBytes = request.PeBytes.ToByteArray();
                var pdbBytes = request.PdbBytes.ToByteArray();

                var asm = Assembly.Load(peBytes, pdbBytes);
                var primaryType = !String.IsNullOrWhiteSpace(request.PrimaryTypeName)
                    ? asm.GetTypes().FirstOrDefault(x => x.Name.EndsWith(request.PrimaryTypeName))
                    : null;

                var req = new ProcessNewAssemblyRequest
                {
                    Assembly = asm,
                    PrimaryType = primaryType
                };

                return await _reloadManager.ProcessNewAssembly(req);
            }
            catch (Exception ex)
            {
                _log($"An error occurred when attempting to load assembly: {request.AssemblyName}");

                return new Outcome
                {
                    Success = false,
                    Messages = {new Message {Message_ = ex.ToString()}}
                };
            }
        }

        public override async Task<Outcome> Exec(ExecuteCommandRequest request, ServerCallContext context)
        {
            try
            {
                return await _reloadManager.ExecuteCommand(new CommandRequest(request.Command, request.Args.ToList()));
            }
            catch (Exception ex)
            {
                _log($"An error occurred when attempting to exec command: {request.Command} with args {request.Args}");
                
                return new Outcome
                {
                    Success = false,
                    Messages = {new Message {Message_ = ex.ToString()}}
                };
            }
        }

        public override async Task SynchronizeDependencies(CachedAssemblyState cachedAssemblyState, IServerStreamWriter<AssemblyReference> responseStream, ServerCallContext context)
        {
            var sw = Stopwatch.StartNew();

            var cache = ToDictionary(cachedAssemblyState);
            var currentAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var asm in currentAssemblies)
                await WriteIfNecessary(asm, responseStream);

            _log($"Finished sending current assemblies in {sw.Elapsed}");
            
            AppDomain.CurrentDomain.AssemblyLoad += async (sender, args) 
                => await WriteIfNecessary(args.LoadedAssembly, responseStream);

            await new TaskCompletionSource<bool>(context.CancellationToken).Task;
        }

        public override async Task RequestCommand(Unit request, IServerStreamWriter<ExecuteCommandRequest> responseStream, ServerCallContext context)
        {
            RequestHostCommandImpl = async cmd =>
            {
                var req = new ExecuteCommandRequest { Command = cmd.Command };
                               
                // woof
                req.Args.AddRange(cmd.Args);
                
                await responseStream.WriteAsync(req);
            };
            
            await new TaskCompletionSource<bool>(context.CancellationToken).Task;
        }
        
        private Func<CommandRequest, Task> RequestHostCommandImpl;

        private Dictionary<string, ulong> ToDictionary(CachedAssemblyState cachedAssemblyState) =>
            cachedAssemblyState.CachedAssemblies?.ToDictionary(
                x => x.AssemblyName, 
                x => x.ModificationTime);

        private async Task WriteIfNecessary(Assembly asm, IServerStreamWriter<AssemblyReference> responseStream)
        {
            _log($"Send {asm.FullName}?");
            var sw = Stopwatch.StartNew();
            
            if (asm.IsDynamic || String.IsNullOrWhiteSpace(asm.Location))
                return;
            
            if (asm.FullName.StartsWith("r2,"))
                return;

            try
            {
                await using var fs = new FileStream(asm.Location, FileMode.Open, FileAccess.Read);

                await responseStream.WriteAsync(new AssemblyReference
                {
                    AssemblyName = asm.FullName,
                    AssemblyLocation = asm.Location,
                    ModificationTime = 
                        (ulong) new DateTimeOffset(new FileInfo(asm.Location).LastWriteTimeUtc, TimeSpan.Zero)
                            .ToUnixTimeSeconds(),
                    
                    PeBytes = await ByteString.FromStreamAsync(fs),
                });

                _log($"Sent {asm.FullName} - {sw.Elapsed}");
            }

            catch (Exception ex)
            {
                _log(ex.ToString());
            }
        }
    }
}