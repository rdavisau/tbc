using UIKit;
using Foundation;
using tbc.sample.ios.Reload;
using Tbc.Target;
using Tbc.Target.Config;

namespace spicy;

[Register("AppDelegate")]
public class AppDelegate : SpiceAppDelegate
{
	public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
	{
		base.FinishedLaunching(application, launchOptions);

		ArgumentNullException.ThrowIfNull(Window);

		var vc = new UIViewController();
		Window.RootViewController = vc;
		Window.MakeKeyAndVisible();

		vc.View!.AddSubview(new App(Window.Frame));

		SetupReload();

		return true;
	}


	private async Task SetupReload()
	{
		var reloadManager = new iOSReloadManager(Window);
		var targetServer = new TargetServer(new TargetConfiguration { ListenPort = 50125 });

		await targetServer.Run(reloadManager);
	}
}
