using tbc.sample.ios.Reload;
using Tbc.Target;
using Tbc.Target.Config;

namespace tbc.sample.ios;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow Window { get; set; } = default!;

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = new MyViewController() };
        Window.MakeKeyAndVisible();

        Task.Run(SetupReload);

        return true;
    }

    private async Task SetupReload()
    {
        var reloadManager = new iOSReloadManager(Window);
        var targetServer = new TargetServer(new TargetConfiguration { ListenPort = 50125 });

        await targetServer.Run(reloadManager);
    }
}
