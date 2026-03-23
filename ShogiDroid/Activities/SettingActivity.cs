using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Java.Interop;
using ShogiGUI;

namespace ShogiDroid;

[Activity(Label = "@string/action_settings", Theme = "@style/AppTheme")]
public class SettingActivity : ThemedPreferenceActivity
{
	// セクション定数
	public const string ExtraSection = "settings_section";
	public const string SectionGame = "game";
	public const string SectionAnalyze = "analyze";
	public const string SectionDisplay = "display";
	public const string SectionControls = "controls";
	public const string SectionUser = "user";
	public const string SectionEngineConnection = "engine_connection";

	public class SettingsFragment : PreferenceFragment, ISharedPreferencesOnSharedPreferenceChangeListener, IJavaObject, IDisposable, IJavaPeerable
	{
		private static readonly string[] SummeryKeys = new string[]
		{
			"Engine.Time", "Engine.Countdown", "Engine.RemoteHost", "Engine.RemotePort", "Analyze.Time", "App.AnimationSpeed", "App.MoveStyle", "App.PlayerName", "App.WarsUserName", "App.PlayInterval",
			"App.CustomMenuButton", "App.ReverseButotn", "App.PVDisplay", "App.ShortcutMenu1", "App.ShortcutMenu2", "App.ShortcutMenu3", "App.ShortcutMenu4", "App.ShortcutMenu5", "App.ShortcutMenu6", "App.ThemeMode"
		};

		public override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			string section = Activity?.Intent?.GetStringExtra(ExtraSection) ?? "";
			int xmlRes = section switch
			{
				SectionGame => Resource.Xml.pref_game,
				SectionAnalyze => Resource.Xml.pref_analyze,
				SectionDisplay => Resource.Xml.pref_display,
				SectionControls => Resource.Xml.pref_controls,
				SectionUser => Resource.Xml.pref_user,
				SectionEngineConnection => Resource.Xml.pref_engine_connection,
				_ => Resource.Xml.fragmented_preferences
			};
			AddPreferencesFromResource(xmlRes);
			string[] summeryKeys = SummeryKeys;
			foreach (string summary in summeryKeys)
			{
				SetSummary(summary);
			}
		}

		public override void OnResume()
		{
			base.OnResume();
			PreferenceScreen.SharedPreferences.RegisterOnSharedPreferenceChangeListener(this);
		}

		public override void OnPause()
		{
			base.OnPause();
			PreferenceScreen.SharedPreferences.UnregisterOnSharedPreferenceChangeListener(this);
		}

		public void OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
		{
			if (SummeryKeys.Contains(key))
			{
				SetSummary(key);
			}
			if (key == "App.ThemeMode")
			{
				string mode = sharedPreferences.GetString(key, "system");
				ThemeHelper.ApplyTheme(mode);
				Activity?.Recreate();
			}
		}

		private void SetSummary(string key)
		{
			Preference preference = PreferenceScreen.FindPreference(key);
			if (preference == null)
			{
				return;
			}
			if (preference.GetType() == typeof(EditTextPreference))
			{
				EditTextPreference editTextPreference = (EditTextPreference)preference;
				preference.Summary = editTextPreference.Text;
				return;
			}
			ListPreference listPreference = (ListPreference)preference;
			int num = listPreference.FindIndexOfValue(listPreference.Value);
			if (num >= 0)
			{
				listPreference.Summary = listPreference.GetEntries()[num];
			}
		}
	}

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		string section = Intent?.GetStringExtra(ExtraSection) ?? "";
		string title = section switch
		{
			SectionGame => "対局設定",
			SectionAnalyze => "解析設定",
			SectionDisplay => "表示設定",
			SectionControls => "操作設定",
			SectionUser => "データ・ユーザー",
			SectionEngineConnection => "リモート接続設定",
			_ => GetString(Resource.String.action_settings)
		};
		Title = title;
		UpdateWindowSettings();
		FragmentManager.BeginTransaction().Replace(16908290, new SettingsFragment()).Commit();
	}

	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);
		if (hasFocus)
		{
			UpdateWindowSettings();
		}
	}

	private void UpdateWindowSettings()
	{
		if (Settings.AppSettings.DispToolbar)
		{
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.Visible;
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
		}
		else
		{
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
				SystemUiFlags.ImmersiveSticky |
				SystemUiFlags.HideNavigation |
				SystemUiFlags.Fullscreen);
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
		}
		AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, Settings.AppSettings.DispToolbar);
	}
}
