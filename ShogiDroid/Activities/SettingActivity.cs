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

[Activity(Label = "@string/action_settings", Theme = "@style/Theme.AppCompat.Light")]
public class SettingActivity : PreferenceActivity
{
	public class SettingsFragment : PreferenceFragment, ISharedPreferencesOnSharedPreferenceChangeListener, IJavaObject, IDisposable, IJavaPeerable
	{
		private static readonly string[] SummeryKeys = new string[17]
		{
			"Engine.Time", "Engine.Countdown", "Analyze.Time", "App.AnimationSpeed", "App.MoveStyle", "App.PlayerName", "App.WarsUserName", "App.PlayInterval",
			"App.CustomMenuButton", "App.ReverseButotn", "App.PVDisplay", "App.ShortcutMenu1", "App.ShortcutMenu2", "App.ShortcutMenu3", "App.ShortcutMenu4", "App.ShortcutMenu5", "App.ShortcutMenu6"
		};

		public override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			AddPreferencesFromResource(Resource.Xml.fragmented_preferences);
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

	private SystemUiFlags uiFlags = SystemUiFlags.Fullscreen;

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
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
			uiFlags = SystemUiFlags.ImmersiveSticky;
		}
		else
		{
			uiFlags = SystemUiFlags.Fullscreen;
		}
		if (Settings.AppSettings.DispToolbar)
		{
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
		}
		else
		{
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
		}
		Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiFlags;
	}
}
