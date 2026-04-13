using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using ShogiGUI;

namespace ShogiDroid;

public abstract class ThemedActivity : Activity
{
	private string appliedThemeMode_ = "system";

	protected override void AttachBaseContext(Context @base)
	{
		base.AttachBaseContext(ThemeHelper.WrapContextForTheme(@base));
	}

	protected override void OnCreate(Bundle savedInstanceState)
	{
		appliedThemeMode_ = ThemeHelper.GetThemeMode();
		base.OnCreate(savedInstanceState);
	}

	protected override void OnResume()
	{
		base.OnResume();
		ReloadThemeIfNeeded();
	}

	protected void ReloadThemeIfNeeded()
	{
		string currentThemeMode = ThemeHelper.GetThemeMode();
		if (appliedThemeMode_ != currentThemeMode)
		{
			appliedThemeMode_ = currentThemeMode;
			Settings.Load();
			Recreate();
		}
	}

	/// <summary>dp 値をピクセルに変換する</summary>
	protected int DpToPx(int dp)
	{
		return (int)(dp * Resources.DisplayMetrics.Density + 0.5f);
	}
}

public abstract class ThemedPreferenceActivity : PreferenceActivity
{
	private string appliedThemeMode_ = "system";

	protected override void AttachBaseContext(Context @base)
	{
		base.AttachBaseContext(ThemeHelper.WrapContextForTheme(@base));
	}

	protected override void OnCreate(Bundle savedInstanceState)
	{
		appliedThemeMode_ = ThemeHelper.GetThemeMode();
		base.OnCreate(savedInstanceState);
	}

	protected override void OnResume()
	{
		base.OnResume();
		ReloadThemeIfNeeded();
	}

	protected void ReloadThemeIfNeeded()
	{
		string currentThemeMode = ThemeHelper.GetThemeMode();
		if (appliedThemeMode_ != currentThemeMode)
		{
			appliedThemeMode_ = currentThemeMode;
			Settings.Load();
			Recreate();
		}
	}
}
