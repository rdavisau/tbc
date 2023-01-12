using DryIoc;
using maui.Pages;
using IContainer = DryIoc.IContainer;

namespace maui;

public partial class App : Application
{
    public App(IContainer container)
    {
        MainPage = container.Resolve<MainPage>();
    }
}
