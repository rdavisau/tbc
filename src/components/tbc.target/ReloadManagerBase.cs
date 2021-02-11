using System;
using System.Threading.Tasks;
using Tbc.Protocol;
using Tbc.Target.Interfaces;
using Tbc.Target.Requests;

namespace Tbc.Target
{
    public abstract class ReloadManagerBase : IReloadManager, INotifyReplacement, IRequestHostCommand
    {
        public abstract Task<Outcome> ProcessNewAssembly(ProcessNewAssemblyRequest req);
        public abstract Task<Outcome> ExecuteCommand(CommandRequest req);

        private Func<CommandRequest, Task> _requestHostCommand;
        private Action<IReloadManager> _notifyReplacement;
        
        Func<CommandRequest, Task> IRequestHostCommand.RequestHostCommand
        {
            get => _requestHostCommand;
            set => _requestHostCommand = value;
        }

        Action<IReloadManager> INotifyReplacement.NotifyReplacement
        {
            get => _notifyReplacement;
            set => _notifyReplacement = value;
        }

        protected virtual void NotifyReplacement(IReloadManager newReloadManager) 
            => _notifyReplacement?.Invoke(newReloadManager);
        
        protected virtual void RequestHostCommand(CommandRequest request) 
            => _requestHostCommand?.Invoke(request);
    }
}