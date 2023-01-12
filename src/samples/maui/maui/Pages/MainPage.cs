using CommunityToolkit.Maui.Markup;
using maui.BaseTypes;

namespace maui.Pages;

public class MainPage : BasePage<MainViewModel>
{
    public MainPage(MainViewModel vm) : base(vm)
    {
        BackgroundColor = Colors.Blue;

        Content =
            new StackLayout
            {
                Children =
                {
                    new Label { Text = vm.MyString }
                       .TextColor(Colors.Red)
                       .TextCenter(),
                }
            }.Center();
    }
}

public class MainViewModel : BaseViewModel
{
    private readonly IMyService _myService;

    public string MyString { get; set; }

    public MainViewModel(IMyService myService)
    {
        _myService = myService;

        MyString = _myService.MyString;
    }
}


public interface IMyService
{
    string MyString { get; }
}
public class MyService : BaseService, IMyService
{
    public string MyString => "service string";
}
