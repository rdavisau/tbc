using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DryIoc;
using Prism.Navigation;
using Prism.Services;
using Serilog;
using Tbc.Core.Models;
using tbc.sample.prism.Extensions;
using Tbc.Target;
using Tbc.Target.Interfaces;
using Tbc.Target.Requests;
using Xamarin.Forms;
using static Tbc.Core.Models.Outcome;

namespace tbc.sample.prism
{
    public class ReloadManager : ReloadManagerBase
    { 
        private readonly IContainer _container;
        private readonly ILogger _logger;
        private readonly Func<INavigationService> _navigationServiceFactory;

        public ReloadManager(IContainer container, ILogger logger, Func<INavigationService> navigationServiceFactory)
        {
            _container = container;
            _logger = logger;
            _navigationServiceFactory = navigationServiceFactory;
        }

        public override async Task<Outcome> ProcessNewAssembly(ProcessNewAssemblyRequest req)
        {
            var types = req.Assembly.GetTypes();
            
            _logger.Information("Types: {Types}", types);
            
            // replace reloader
            var reloaderType = types.Where(x => x.ImplementsServiceType<IReloadManager>() && x != GetType()).LastOrDefault();
            if (reloaderType != null)
            {
                _container.Register(typeof(IReloadManager), reloaderType, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
                var newReloader = _container.Resolve<IReloadManager>();
                
                NotifyReplacement(newReloader);

                return await newReloader.ProcessNewAssembly(req);
            }

            // replace services
            foreach (var serviceType in types.Where(t => t.IsSubclassOf(typeof(ServiceBase)) && !t.IsAbstract))
            {
                _logger.Information("Registering service type {PageType} from assembly {Assembly}", serviceType, req.Assembly);
                
                foreach (var @if in serviceType.GetImplementedInterfaces())
                {
                    _logger.Information("Registering service type {ImplementationType} against {ServiceType} from assembly {Assembly}",
                        serviceType, @if, req.Assembly);

                    _container.Register(
                        @if,
                        serviceType,
                        Reuse.Singleton,
                        ifAlreadyRegistered: IfAlreadyRegistered.Replace,
                        setup: Setup.With(asResolutionCall: true)
                    );
                }
            }
            
            // replace viewmodels
            foreach (var vm in types.Where(t => t.IsSubclassOf(typeof(ViewModelBase)) && !t.IsAbstract))
            {
                _logger.Information("Registering view model type {PageType} from assembly {Assembly}", vm, req.Assembly);
                _container.Register(vm, ifAlreadyRegistered: IfAlreadyRegistered.Replace, setup: Setup.With(asResolutionCall:true));
            }
            
            // replace pages
            foreach (var page in types.Where(p => p.IsSubclassOf(typeof(ContentPage))))
            {
                _logger.Information("Registering page type {PageType} from assembly {Assembly}", page, req.Assembly);
                
                _container.Register(typeof(object), page, serviceKey: page.Name, made: Made.Of(FactoryMethod.ConstructorWithResolvableArguments), ifAlreadyRegistered: IfAlreadyRegistered.Replace);
            }

            // go to the primary type
            var primaryType = req.PrimaryType ?? types.FirstOrDefault(x => x.IsSubclassOf(typeof(ContentPage)));
            if (primaryType != null && primaryType.IsSubclassOf(typeof(ContentPage)))
            {
                _logger.Information("Navigate to {PrimaryType}", primaryType);
                    
                await Device.InvokeOnMainThreadAsync(
                    () => _navigationServiceFactory().NavigateAsync(primaryType.Name, new NavigationParameters(), null, animated: false));
            }

            return new Outcome { Success = true };
        }

        public override async Task<Outcome> ExecuteCommand(CommandRequest commandRequest)
        {
            var (command, args) = (commandRequest.Command, commandRequest.Args);
                        
            _logger.Information("Execute Command \"{Command}\" with args {@Args}", command, args);
            
            switch (command)
            {                
                case "goto":
                    await Device.InvokeOnMainThreadAsync(
                        async () => await GotoPage(args.Any() ? args[0] : null, message:"where to my friend?"));
                    return new Outcome { Success = true };
                                                
                default:
                    _logger.Warning("No logic to handle command \"{Command}\" with args {@Args}", command, args);
                    return new Outcome
                    {
                        Success = true,
                        Messages = new List<OutcomeMessage>()
                        {
                            new OutcomeMessage
                            {
                                Message =
                                    $"No logic to handle command \"{command}\" with args {String.Join(", ", args)}"
                            }
                        }
                    };
            }
        }

        private async Task GotoPage(string pageName = null, string message = "choose")
        {
            var pageDialogService = _container.Resolve<IPageDialogService>();
            var navigationService = _container.Resolve<INavigationService>();

            if (String.IsNullOrWhiteSpace(pageName))
            {
                var pages =
                    _container.GetServiceRegistrations()
                        .Where(x => x.ImplementationType.IsSubclassOf(typeof(ContentPage)))
                        .GroupBy(x => x.ImplementationType.FullName)
                        .Select(x => x.OrderByDescending(y => y.FactoryRegistrationOrder).First())
                        .ToList();
                
                var choice = await pageDialogService.PresentChoice(message, pages, x => x.ImplementationType.Name);
                if (choice.ServiceType != null)
                    pageName = choice.ImplementationType?.Name;
            }

            if (String.IsNullOrWhiteSpace(pageName))
            {
                _logger.Warning("No page chosen for navigation");
                return;
            }

            await navigationService.NavigateAsync(pageName);
        }
    }
}
