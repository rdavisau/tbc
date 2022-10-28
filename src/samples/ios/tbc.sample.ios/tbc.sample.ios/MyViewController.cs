namespace tbc.sample.ios;

// We need this 'register' hack on top of any NSObject-derived type (for now),
// and it should be different for each type (for now)
// and it only works on the currently edited file (for now)
[Register("_MYGUID_MYGUID_MYGUID_MYGUID_MYGUID_")]
public class MyViewController : UIViewController
{
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        View!.AddSubview(new UILabel(UIApplication.SharedApplication.Delegate.GetWindow().Frame)
        {
            Alpha = new nfloat(0),
            Lines = 0,
            BackgroundColor = UIColor.Yellow,
            TextAlignment = UITextAlignment.Center,
            Text = "Hello, iOS",
        });

        UIView.Animate(.25, () => View.Alpha = new nfloat(1.0f));
    }
}
