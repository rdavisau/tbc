using System;
using System.Linq;
using Xamarin.CommunityToolkit.Markup;
using Xamarin.Forms;

namespace tbc.sample.prism
{
    public class MainPage : PageBase<MainViewModel>
    {        
        public MainPage(MainViewModel viewModel) : base(viewModel)
        {
            BackgroundColor = Color.WhiteSmoke;
            
            Content =
                new StackLayout
                {
                    Children =
                    { 
                        new Label { Text = ViewModel.Text }
                            .TextCenter(),

                        new Label { }
                            .TextCenter()
                            .Bind(nameof(ViewModel.TextFromTheService)),
                        
                        new BoxView { BackgroundColor = Color.Goldenrod }
                            .Size(50)
                            .Center(),
                        
                        new BoxView { BackgroundColor = Color.Goldenrod }
                            .Size(50)
                            .Center(),
                        
                        new BoxView { BackgroundColor = Color.Goldenrod }
                            .Size(50)
                            .Center(),
                    }
                }
                .CenterVertical()
                .FillHorizontal();
        }
    }
}