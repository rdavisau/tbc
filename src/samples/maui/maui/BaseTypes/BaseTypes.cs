namespace maui.BaseTypes;

// imagine these were good page base classes
public abstract class BasePage : ContentPage { }
public abstract class BasePage<TViewModel> : BasePage
    where TViewModel : BaseViewModel
{
    public TViewModel ViewModel { get; set; }

    public BasePage(TViewModel viewModel)
    {
        ViewModel = viewModel;
    }
}

// imagine this was a good view model base class with inotify etc.
public abstract class BaseViewModel
{

}

// imagine this was a good view model base class
public abstract class BaseService
{

}
