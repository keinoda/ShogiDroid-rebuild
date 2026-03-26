using AndroidX.AppCompat.App;

namespace ShogiGUI;

public static class ThemeHelper
{
	public static void ApplyTheme(string mode)
	{
		switch (mode)
		{
		case "light":
			AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightNo;
			break;
		case "dark":
			AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightYes;
			break;
		default:
			AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightFollowSystem;
			break;
		}
	}
}
