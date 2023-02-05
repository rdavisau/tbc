using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Config;

namespace Tbc.Host
{
    public static class Configurator
    {
        public static readonly Dictionary<string, Type> KnownConfigMappings = new Dictionary<string, Type>
        {
            [KnownConfigurationKeys.FileWatch] = typeof(FileWatchConfig),
            [KnownConfigurationKeys.FileEnvironment] = typeof(FileEnvironmentConfig),
            [KnownConfigurationKeys.AssemblyCompilation] = typeof(AssemblyCompilationOptions),
            [KnownConfigurationKeys.ServiceDiscovery] = typeof(object),
        };

        public static Container ConfigureServices(Dictionary<string, JObject> config, params Assembly[] withAssemblies)
            => ConfigureServices(config, _ => { }, withAssemblies);

        public static Container ConfigureServices(Dictionary<string, JObject> config, Action<Container> configure, params Assembly[] withAssemblies)
        {            
            var services = new Container(rules => rules.WithoutThrowOnRegisteringDisposableTransient()
                    .With(Made.Of(FactoryMethod.ConstructorWithResolvableArguments))
                    .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace)
                    .WithoutThrowOnRegisteringDisposableTransient()
                    .WithFuncAndLazyWithoutRegistration()
                    .WithAutoConcreteTypeResolution());

            AddComponents(services, withAssemblies);
            AddConfiguration(services, config);
            AddModuleRegistrations(services);
            AddAbstractions(services);
            AddLogging(services);
            
            configure(services);
            
            return services;
        }
        
        private static void AddModuleRegistrations(Container services)
        {
        }

        private static void AddAbstractions(Container services)
        {
            services.Register<IFileSystem, FileSystem>();
        }

        private static void AddComponents(Container services, Assembly[] withAssemblies)
        {
            var components =
                Enumerable
                    .Concat(
                        typeof(ComponentBase).Assembly.GetTypes(),
                        withAssemblies.SelectMany(asm => asm.GetTypes()))
                    .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(ComponentBase)));
            
            foreach (var c in components)
            foreach (var @if in c.GetInterfaces())
                services.Register(@if, c, 
                    c.ImplementsServiceType(typeof(ITransientComponent)) ? Reuse.Transient : Reuse.Singleton);
        }

        private static void AddConfiguration(Container container, Dictionary<string, JObject> config)
        {
            container.RegisterInstance(config);
            
            foreach (var configMapping in KnownConfigMappings)
                if (config.TryGetValue(configMapping.Key, out var opts))
                    container.RegisterInstance(configMapping.Value, opts.ToObject(configMapping.Value));
        }

        private static void AddLogging(Container container)
        {
            foreach (var registration in new ServiceCollection().AddLogging(x => x.AddConsole()))
                if (registration.ImplementationInstance != null)
                    container.RegisterInstance(registration.ServiceType, registration.ImplementationInstance, IfAlreadyRegistered.Replace);
                else
                    container.Register(registration.ServiceType, registration.ImplementationType);
        }
    }
}