using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using Prism;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation;
using Serilog;
using Tbc.Target;
using Tbc.Target.Config;
using Tbc.Target.Interfaces;
using Xamarin.Forms;

namespace tbc.sample.prism
{
    public class App : PrismApplicationBase
    {        
        public IContainerExtension Extension { get; set; }
        

        protected override IContainerExtension CreateContainerExtension()
        {
            if (Extension != null)
                return Extension;

            try { Extension = new DryIocContainerExtension(CreateContainer()); }
            catch (InvalidOperationException ex) { Extension = ContainerLocator.Current; }

            return Extension;
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry) { }

        protected override async void OnInitialized()
        {
            var navigationService = Container.Resolve<INavigationService>();

            await navigationService.NavigateAsync("MainPage");

            var reloadManager = Container.Resolve<IReloadManager>();
            var targetServer = new TargetServer(new TargetConfiguration { ListenPort = 50124 });
            await targetServer.Run(reloadManager);
        }

        public IContainer CreateContainer()
        {
            var assemblies = new List<Assembly>() {GetType().Assembly};
            
            var container = new DryIoc.Container(rules =>
            {
                rules.WithoutThrowOnRegisteringDisposableTransient()
                    .WithAutoConcreteTypeResolution()
                    .With(FactoryMethod.ConstructorWithResolvableArguments)
                    .WithoutThrowOnRegisteringDisposableTransient()
                    .WithFuncAndLazyWithoutRegistration()
                    .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace);

                if (Device.RuntimePlatform == Device.iOS)
                    rules = rules.WithoutFastExpressionCompiler();

                return rules;
            });

            SetupLogging(container);
            RegisterPages(container, assemblies);
            RegisterServices(container, assemblies);
            RegisterViewModels(container, assemblies);

            container.Register<IReloadManager, ReloadManager>();
            
            return container;
        }

        private void SetupLogging(IContainer container)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Debug(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {SourceContext}::{Level:u3} ] {Message:lj} {NewLine}{Exception}")
                .CreateLogger();
            
            container.RegisterInstance(Log.Logger);
        }

        private void RegisterViewModels(IContainer container, List<Assembly> assemblies)
        {
            // allow runtime re-registration after resolve for hot reload of services
            var asResolutionCall = false;
#if DEBUG
            asResolutionCall = true;
#endif
            var serviceBase = typeof(ViewModelBase);
            var viewModelTypes = GetTypesFromAssemblies(assemblies, t => !t.IsAbstract && t.IsSubclassOf(serviceBase));

            foreach (var viewModel in viewModelTypes)
                container.Register(viewModel, reuse: Reuse.Transient, setup: Setup.With(asResolutionCall: asResolutionCall));
        }
        
        private void RegisterServices(IContainer container, List<Assembly> assemblies)
        {
            var asResolutionCall = false;
#if DEBUG
            // allow runtime re-registration after resolve for hot reload of services
            asResolutionCall = true;
#endif
            var serviceBase = typeof(ServiceBase);
            var serviceTypes = GetTypesFromAssemblies(assemblies, t => !t.IsAbstract && t.IsSubclassOf(serviceBase));

            foreach (var serviceType in serviceTypes)
            foreach (var @if in serviceType.GetImplementedInterfaces())
                container.Register(
                    @if,
                    serviceType,
                    Reuse.Singleton,
                    ifAlreadyRegistered: IfAlreadyRegistered.Replace,
                    setup: Setup.With(asResolutionCall: asResolutionCall)
                );
        }

        private void RegisterPages(IContainer container, List<Assembly> assemblies)
        {
            var pages =
                GetTypesFromAssemblies(assemblies,
                    x => x.IsSubclassOf(typeof(ContentPage)))
                    .Where(x => !x.IsAbstract);

            foreach (var page in pages)
            {
                container.Register(page, made: Made.Of(FactoryMethod.ConstructorWithResolvableArguments));
                container.Register(typeof(object), page, serviceKey: page.Name, made: Made.Of(FactoryMethod.ConstructorWithResolvableArguments));
            }
            
            container.Register(typeof(object), typeof(NavigationPage), serviceKey: nameof(NavigationPage), made: Made.Of(FactoryMethod.ConstructorWithResolvableArguments));
        }
        
        private readonly Dictionary<Assembly, Type[]> _asmTypes = new Dictionary<Assembly, Type[]>();
        public IEnumerable<Type> GetTypesFromAssemblies(List<Assembly> assemblies, Func<Type, bool> predicate)
        {
            foreach (var asm in assemblies)
            {
                if (!_asmTypes.TryGetValue(asm, out var types))
                {
                    types = asm.GetTypes();
                    _asmTypes[asm] = types;
                }
                
                foreach (var t in types)
                    if (predicate(t))
                        yield return t;
            }
        }
    }
}