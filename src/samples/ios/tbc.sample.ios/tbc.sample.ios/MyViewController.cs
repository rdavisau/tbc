namespace tbc.sample.ios;

public class MyViewController : UIViewController
{
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        View!.AddSubview(new UILabel(UIApplication.SharedApplication.Delegate.GetWindow().Frame)
        {
            BackgroundColor = UIColor.Green,
            TextAlignment = UITextAlignment.Center,
            Text = "Hello, iOS",
        });
    }
}

