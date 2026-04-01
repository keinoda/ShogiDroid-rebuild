using System;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Interop;
using ShogiGUI;
using IOPath = System.IO.Path;

namespace ShogiDroid;

[Activity(Label = "@string/action_settings", Theme = "@style/AppTheme")]
public class SettingActivity : PreferenceActivity
{
	// セクション定数
	public const string ExtraSection = "settings_section";
	public const string SectionGame = "game";
	public const string SectionAnalyze = "analyze";
	public const string SectionDisplay = "display";
	public const string SectionControls = "controls";
	public const string SectionUser = "user";
	public const string SectionEngineConnection = "engine_connection";
	public const string ActionExportSettings = "Action.ExportSettings";
	public const string ActionImportSettings = "Action.ImportSettings";

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
				_ => Resource.Xml.pref_user
			};
			AddPreferencesFromResource(xmlRes);
			SetActionSummaries();
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

		public override bool OnPreferenceTreeClick(PreferenceScreen preferenceScreen, Preference preference)
		{
			string key = preference?.Key ?? string.Empty;
			if ((Activity as SettingActivity)?.HandleActionPreferenceClick(key) == true)
			{
				return true;
			}
			return base.OnPreferenceTreeClick(preferenceScreen, preference);
		}

		private void SetActionSummaries()
		{
			string path = Settings.GetBackupFilePath();
			SetStaticSummary(ActionExportSettings, string.Format(Activity?.GetString(Resource.String.SettingsExportSummary_Text) ?? string.Empty, path));
			SetStaticSummary(ActionImportSettings, string.Format(Activity?.GetString(Resource.String.SettingsImportSummary_Text) ?? string.Empty, path));
		}

		private void SetStaticSummary(string key, string summary)
		{
			Preference preference = PreferenceScreen.FindPreference(key);
			if (preference != null)
			{
				preference.Summary = summary;
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
		if (string.IsNullOrEmpty(section))
		{
			StartActivity(new Intent(this, typeof(SettingsHomeActivity)));
			Finish();
			return;
		}

		string title = section switch
		{
			SectionGame => "対局設定",
			SectionAnalyze => "解析設定",
			SectionDisplay => "表示設定",
			SectionControls => "操作・カスタマイズ",
			SectionUser => "データ・ユーザー",
			SectionEngineConnection => "リモート接続設定",
			_ => GetString(Resource.String.action_settings)
		};
		Title = title;
		UpdateWindowSettings();
		FragmentManager.BeginTransaction().Replace(16908290, new SettingsFragment()).Commit();
	}

	public bool HandleActionPreferenceClick(string key)
	{
		switch (key)
		{
		case ActionExportSettings:
			ExportSettings();
			return true;
		case ActionImportSettings:
			ConfirmImportSettings();
			return true;
		default:
			return false;
		}
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

	private void ExportSettings()
	{
		LocalFile.CreateFolders();
		string path = Settings.GetBackupFilePath();
		if (Settings.ExportToFile(path, out string errorMessage))
		{
			Toast.MakeText(
				this,
				string.Format(GetString(Resource.String.SettingsExportCompleted_Text), IOPath.GetFileName(path)),
				ToastLength.Long).Show();
			return;
		}

		Toast.MakeText(
			this,
			string.Format(GetString(Resource.String.SettingsTransferFailed_Text), errorMessage),
			ToastLength.Long).Show();
	}

	private void ConfirmImportSettings()
	{
		string path = Settings.GetBackupFilePath();
		if (!File.Exists(path))
		{
			Toast.MakeText(this, GetString(Resource.String.SettingsImportMissing_Text), ToastLength.Long).Show();
			return;
		}

		new AlertDialog.Builder(this)
			.SetTitle(Resource.String.SettingsImportConfirmTitle_Text)
			.SetMessage(string.Format(GetString(Resource.String.SettingsImportConfirmMessage_Text), IOPath.GetFileName(path)))
			.SetPositiveButton(Resource.String.DialogOK_Text, (sender, args) => ImportSettings(path))
			.SetNegativeButton(Resource.String.DialogCancel_Text, (sender, args) => { })
			.Show();
	}

	private void ImportSettings(string path)
	{
		if (!Settings.ImportFromFile(path, out string errorMessage))
		{
			Toast.MakeText(
				this,
				string.Format(GetString(Resource.String.SettingsTransferFailed_Text), errorMessage),
				ToastLength.Long).Show();
			return;
		}

		ThemeHelper.ApplyTheme(Settings.AppSettings.ThemeMode);
		UpdateWindowSettings();
		Toast.MakeText(this, GetString(Resource.String.SettingsImportCompleted_Text), ToastLength.Long).Show();
		Recreate();
	}
}
