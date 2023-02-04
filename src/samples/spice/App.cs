using CoreGraphics;

namespace spicy;

public class App : Application
{
	public App(CGRect frame) : base(frame)
	{
		int count = 0;

		var label = SpicyLabel("Spicy? 🌶");

		var button = new Button
		{
			Text = "Click Me for extra spice",
			Clicked = _ => label.Text = String.Join("", Enumerable.Repeat("🌶", ++count))
		};

		Main = new StackView
		{
			label, button, SpicyLabel(),
		};
	}

	private static Label SpicyLabel(string text =  "🌶")
	{
		var label = new Label
		{
			Text = text
		};
		return label;
	}
}


