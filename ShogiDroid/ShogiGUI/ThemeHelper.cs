using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Preferences;
using AndroidX.AppCompat.App;

namespace ShogiGUI;

public static class ThemeHelper
{
	public static string GetThemeMode()
	{
		try
		{
			var context = Application.Context;
			if (context != null)
			{
				return PreferenceManager.GetDefaultSharedPreferences(context)
					.GetString("App.ThemeMode", Settings.AppSettings.ThemeMode ?? "system") ?? "system";
			}
		}
		catch
		{
		}

		return string.IsNullOrEmpty(Settings.AppSettings.ThemeMode) ? "system" : Settings.AppSettings.ThemeMode;
	}

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

	public static Context WrapContextForTheme(Context baseContext)
	{
		if (baseContext == null)
		{
			return baseContext;
		}

		string mode = GetThemeMode();
		ApplyTheme(mode);

		UiMode? nightMode = mode switch
		{
			"light" => UiMode.NightNo,
			"dark" => UiMode.NightYes,
			_ => null
		};

		if (!nightMode.HasValue)
		{
			return baseContext;
		}

		var config = new Configuration();
		config.SetTo(baseContext.Resources.Configuration);
		config.UiMode = (UiMode)(((int)config.UiMode & ~(int)UiMode.NightMask) | (int)nightMode.Value);
		return baseContext.CreateConfigurationContext(config);
	}
}
