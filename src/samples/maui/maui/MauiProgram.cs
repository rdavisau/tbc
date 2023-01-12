using CommunityToolkit.Maui.Markup;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using maui.Reload;
using Microsoft.Extensions.Logging;
using Tbc.Target.Config;
using Debug = System.Diagnostics.Debug;
using IContainer = DryIoc.IContainer;

namespace maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        var container = ConfigureMutableContainer(builder);

        builder
           .UseMauiApp<App>()
           .UseMauiCommunityToolkitMarkup()
           .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

#if DEBUG
        Task.Run(() => RunTbc(app, container));
#endif
        return app;
    }

    private static async Task RunTbc(MauiApp app, IContainer container)
    {
        var rm = new ReloadManager(container);
        var ts = new Tbc.Target.TargetServer(TargetConfiguration.Default(port: 50129));
        await ts.Run(rm, x => Debug.WriteLine(x));
    }

    private static IContainer ConfigureMutableContainer(MauiAppBuilder builder)
    {
        // this is the dryioc container
        var container =
            new Container(Rules.MicrosoftDependencyInjectionRules)
               .RegisterApplicationTypes(typeof(MauiProgram).Assembly.GetTypes());

        // this makes it maui friendly
        builder.ConfigureContainer(new DryIocServiceProviderFactory(container));

        return container;
    }
}
