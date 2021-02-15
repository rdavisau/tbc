using System.Threading.Tasks;
using Prism.Navigation;
using Xamarin.Forms;

namespace tbc.sample.prism
{
    public class MainViewModel : ViewModelBase, IInitializeAsync
    {
        private readonly IMyService _myService;
        
        public string Text { get; set; } = "hello";

        private string _textFromTheService;
        public string TextFromTheService
        {
            get => _textFromTheService;
            set => SetProperty(ref _textFromTheService, value);
        }

        public MainViewModel(IMyService myService)
        {
            _myService = myService;
            
            SetTheStringFromTheService("...");
        }
        
        public async Task InitializeAsync(INavigationParameters parameters)
        {
            Device.InvokeOnMainThreadAsync(async () => 
                SetTheStringFromTheService(await _myService.GetAString())); 
        }

        private void SetTheStringFromTheService(string theString)
        {
            TextFromTheService = $"from the service: {theString}";
        }
    }
}