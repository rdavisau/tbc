using Xamarin.Forms;

namespace tbc.sample.prism
{
    public abstract class PageBase<TViewModel> : ContentPage
    {
        public PageBase(TViewModel viewModel)
        {
            ViewModel = viewModel;
        }
        
        public TViewModel ViewModel
        {
            get { return (TViewModel) BindingContext; }
            set { BindingContext = value; }
        }    
    }
}