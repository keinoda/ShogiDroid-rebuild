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
	public const string ActionExportSettings = "Action.ExportSettings";
	public const string ActionImportSettings = "Action.ImportSettings";
	public const string ActionPickSshPrivateKey = "Engine.SshPrivateKeyPath";
	public const string ActionPickSshPublicKey = "Engine.SshPublicKeyPath";
	public const string ActionClearSshSettings = "Engine.ClearSshSettings";
	public const string ActionRemoteCpuCores = "Engine.RemoteCpuCores";
	public const string ActionRemoteRamMb = "Engine.RemoteRamMb";

	private const int RequestPickSshPrivateKey = 9001;
	private const int RequestPickSshPublicKey = 9002;

	public class SettingsFragment : PreferenceFragment, ISharedPreferencesOnSharedPreferenceChangeListener, IJavaObject, IDisposable, IJavaPeerable
	{
		private const string HideInternalEnginePreferenceKey = "App.HideInternalEngine";
#if CLASSIC_UI
		private CompactListScrollListener compactListScrollListener_;
#endif

		private static readonly string[] SummeryKeys = new string[]
		{
			"Engine.Time", "Engine.Countdown", "Engine.RemoteHost", "Engine.RemotePort", "Analyze.Time", "App.AnimationSpeed", "App.MoveStyle", "App.PlayerName", "App.WarsUserName", "App.PlayInterval",
			"App.CustomMenuButton", "App.ReverseButotn", "App.PVDisplay", "App.ShortcutMenu1", "App.ShortcutMenu2", "App.ShortcutMenu3", "App.ShortcutMenu4", "App.ShortcutMenu5", "App.ShortcutMenu6", "App.ThemeMode"
		};

		public override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			string section = Activity?.Intent?.GetStringExtra(ExtraSection) ?? "";
#if CLASSIC_UI
			if (string.IsNullOrEmpty(section))
			{
				AddClassicPreferences();
			}
			else
#endif
			{
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
			}
			SetActionSummaries();
			string[] summeryKeys = SummeryKeys;
			foreach (string summary in summeryKeys)
			{
				SetSummary(summary);
			}
		}

#if CLASSIC_UI
		public override void OnActivityCreated(Bundle savedInstanceState)
		{
			base.OnActivityCreated(savedInstanceState);
			ConfigureClassicPreferenceDensity();
		}
#endif

#if CLASSIC_UI
		private void AddClassicPreferences()
		{
			AddPreferencesFromResource(Resource.Xml.pref_game);
			AddPreferencesFromResource(Resource.Xml.pref_analyze);
			AddPreferencesFromResource(Resource.Xml.pref_display);
			AddPreferencesFromResource(Resource.Xml.pref_controls);
			AddPreferencesFromResource(Resource.Xml.pref_user);
			AddPreferencesFromResource(Resource.Xml.pref_engine_connection);
			AddHideInternalEnginePreference();
		}

		private void AddHideInternalEnginePreference()
		{
			if (PreferenceScreen == null || PreferenceScreen.FindPreference(HideInternalEnginePreferenceKey) != null)
			{
				return;
			}

			var category = new PreferenceCategory(Activity)
			{
				Title = Activity?.GetString(Resource.String.SettingsEngineSelection_Text)
			};
			PreferenceScreen.AddPreference(category);

			var preference = new CheckBoxPreference(Activity)
			{
				Key = HideInternalEnginePreferenceKey,
				Title = Activity?.GetString(Resource.String.SettingsHideInternalEngine_Text),
				Summary = Activity?.GetString(Resource.String.SettingsHideInternalEngine_Summary),
				Persistent = true
			};
			preference.SetDefaultValue(false);
			category.AddPreference(preference);
		}

		private void ConfigureClassicPreferenceDensity()
		{
			var listView = Activity?.FindViewById<ListView>(Android.Resource.Id.List);
			if (listView == null)
			{
				return;
			}

			listView.DividerHeight = Dp(1);
			listView.SetPadding(0, Dp(2), 0, Dp(2));
			listView.SetClipToPadding(false);

			compactListScrollListener_ ??= new CompactListScrollListener(this);
			listView.SetOnScrollListener(compactListScrollListener_);
			listView.Post(() => ApplyCompactDensity(listView));
		}

		private void ApplyCompactDensity(View view)
		{
			if (view == null)
			{
				return;
			}

			if (view is TextView textView)
			{
				if (textView.Id == Android.Resource.Id.Title)
				{
					bool isCategory = textView.Parent is AbsListView;
					textView.SetTextSize(Android.Util.ComplexUnitType.Sp, isCategory ? 12.5f : 14f);
					if (isCategory)
					{
						textView.SetPadding(Dp(10), Dp(8), Dp(10), Dp(4));
					}
				}
				else if (textView.Id == Android.Resource.Id.Summary)
				{
					textView.SetTextSize(Android.Util.ComplexUnitType.Sp, 11.5f);
				}
				return;
			}

			if (view is not ViewGroup group)
			{
				return;
			}

			if (group.Parent is AbsListView)
			{
				group.SetMinimumHeight(0);
				group.SetPadding(0, 0, 0, 0);
			}
			else if (group.Id == Android.Resource.Id.WidgetFrame)
			{
				group.SetMinimumHeight(0);
				group.SetPadding(group.PaddingLeft, Dp(4), Dp(8), Dp(4));
			}
			else if (HasDirectPreferenceText(group))
			{
				group.SetMinimumHeight(0);
				group.SetPadding(Dp(14), Dp(8), Dp(14), Dp(8));
			}

			for (int i = 0; i < group.ChildCount; i++)
			{
				ApplyCompactDensity(group.GetChildAt(i));
			}
		}

		private static bool HasDirectPreferenceText(ViewGroup group)
		{
			for (int i = 0; i < group.ChildCount; i++)
			{
				int childId = group.GetChildAt(i).Id;
				if (childId == Android.Resource.Id.Title || childId == Android.Resource.Id.Summary)
				{
					return true;
				}
			}
			return false;
		}

		private int Dp(int dp)
		{
			return (int)(dp * Resources.DisplayMetrics.Density + 0.5f);
		}

		private sealed class CompactListScrollListener : Java.Lang.Object, AbsListView.IOnScrollListener
		{
			private readonly SettingsFragment owner_;

			public CompactListScrollListener(SettingsFragment owner)
			{
				owner_ = owner;
			}

			public void OnScroll(AbsListView view, int firstVisibleItem, int visibleItemCount, int totalItemCount)
			{
				owner_.ApplyCompactDensity(view);
			}

			public void OnScrollStateChanged(AbsListView view, [GeneratedEnum] ScrollState scrollState)
			{
			}
		}
#endif

		public override void OnResume()
		{
			base.OnResume();
			PreferenceScreen.SharedPreferences.RegisterOnSharedPreferenceChangeListener(this);
#if CLASSIC_UI
			ConfigureClassicPreferenceDensity();
#endif
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
			RefreshSshKeySummaries();
			RefreshRemoteSpecSummaries();
		}

		internal void RefreshSshKeySummaries()
		{
			string defaultHintPrivate = Activity?.GetString(Resource.String.SettingsSshPrivateKeySummary_Text) ?? "タップしてファイルを選択";
			string defaultHintPublic = Activity?.GetString(Resource.String.SettingsSshPublicKeySummary_Text) ?? "タップしてファイルを選択";

			string privatePath = Settings.EngineSettings.VastAiSshKeyPath;
			SetStaticSummary(ActionPickSshPrivateKey,
				string.IsNullOrEmpty(privatePath) ? defaultHintPrivate : privatePath);

			string publicPath = Settings.EngineSettings.SshPublicKeyPath;
			SetStaticSummary(ActionPickSshPublicKey,
				string.IsNullOrEmpty(publicPath) ? defaultHintPublic : publicPath);
		}

		internal void RefreshRemoteSpecSummaries()
		{
			int cores = Settings.EngineSettings.VastAiCpuCores;
			int ramMb = Settings.EngineSettings.VastAiRamMb;
			SetStaticSummary(ActionRemoteCpuCores,
				cores > 0 ? $"{cores} コア" : "自動設定に使用（0=自動設定しない）");
			SetStaticSummary(ActionRemoteRamMb,
				ramMb > 0 ? $"{ramMb} MB" : "自動設定に使用（0=自動設定しない）");
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
#if !CLASSIC_UI
		if (string.IsNullOrEmpty(section))
		{
			StartActivity(new Intent(this, typeof(SettingsHomeActivity)));
			Finish();
			return;
		}
#endif

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
		case ActionPickSshPrivateKey:
			LaunchSshKeyPicker(RequestPickSshPrivateKey);
			return true;
		case ActionPickSshPublicKey:
			LaunchSshKeyPicker(RequestPickSshPublicKey);
			return true;
		case ActionClearSshSettings:
			ClearSshSettings();
			return true;
		case ActionRemoteCpuCores:
			ShowNumberInputDialog("CPUコア数", Settings.EngineSettings.VastAiCpuCores, val =>
			{
				Settings.EngineSettings.VastAiCpuCores = val;
				Settings.Save();
				RefreshSpecSummaries();
			});
			return true;
		case ActionRemoteRamMb:
			ShowNumberInputDialog("メモリ (MB)", Settings.EngineSettings.VastAiRamMb, val =>
			{
				Settings.EngineSettings.VastAiRamMb = val;
				Settings.Save();
				RefreshSpecSummaries();
			});
			return true;
		default:
			return false;
		}
	}

	private void ShowNumberInputDialog(string title, int currentValue, Action<int> onOk)
	{
		var input = new EditText(this);
		input.InputType = Android.Text.InputTypes.ClassNumber;
		input.Text = currentValue.ToString();
		input.SetSelection(input.Text.Length);

		new AlertDialog.Builder(this)
			.SetTitle(title)
			.SetView(input)
			.SetPositiveButton("OK", (s, e) =>
			{
				if (int.TryParse(input.Text, out int val) && val >= 0)
					onOk(val);
			})
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
	}

	private void RefreshSpecSummaries()
	{
		if (FragmentManager.FindFragmentById(16908290) is SettingsFragment fragment)
			fragment.RefreshRemoteSpecSummaries();
	}

	private void ClearSshSettings()
	{
		new AlertDialog.Builder(this)
			.SetTitle("TCP接続モードに切り替え")
			.SetMessage("SSHポート・エンジンコマンドをリセットして、TCP接続モード（IP:Port のみ）に切り替えますか？\n\nSSH鍵は保持されるので、クラウド接続時にはそのまま使えます。")
			.SetPositiveButton("切り替え", (s, e) =>
			{
				// 鍵パスは残す。TCP/SSH 判定に使われるフィールドだけリセット。
				Settings.EngineSettings.VastAiSshPort = 0;
				Settings.EngineSettings.VastAiSshEngineCommand = string.Empty;
				Settings.Save();

				Toast.MakeText(this, "TCP接続モードに切り替えました", ToastLength.Short).Show();
			})
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
	}

	private void LaunchSshKeyPicker(int requestCode)
	{
		var intent = new Intent(Intent.ActionOpenDocument);
		intent.AddCategory(Intent.CategoryOpenable);
		intent.SetType("*/*");
		try
		{
			StartActivityForResult(intent, requestCode);
		}
		catch (Exception ex)
		{
			Toast.MakeText(this, $"ファイル選択画面を開けませんでした: {ex.Message}", ToastLength.Long).Show();
		}
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
	{
		base.OnActivityResult(requestCode, resultCode, data);
		if (resultCode != Result.Ok || data?.Data == null)
			return;

		bool isPublic;
		if (requestCode == RequestPickSshPrivateKey) isPublic = false;
		else if (requestCode == RequestPickSshPublicKey) isPublic = true;
		else return;

		try
		{
			var uri = data.Data;
			string destDir = IOPath.Combine(FilesDir.AbsolutePath, "ssh");
			Directory.CreateDirectory(destDir);

			string originalName = ResolveFileNameFromUri(uri);
			if (string.IsNullOrEmpty(originalName))
				originalName = isPublic ? "id_rsa.pub" : "id_rsa";

			string destPath = IOPath.Combine(destDir, originalName);

			// 同名ファイルがあれば必ず上書き。秘密鍵の read-only 属性を一旦解除して削除する。
			if (File.Exists(destPath))
			{
				try
				{
					var existing = new Java.IO.File(destPath);
					existing.SetWritable(true, true);
					File.Delete(destPath);
				}
				catch (Exception delEx)
				{
					AppDebug.Log.Info($"SettingActivity: 既存鍵ファイル削除失敗（続行）: {delEx.Message}");
				}
			}

			using (var input = ContentResolver.OpenInputStream(uri))
			using (var output = new FileStream(destPath, FileMode.Create))
			{
				input.CopyTo(output);
			}

			var file = new Java.IO.File(destPath);
			if (isPublic)
			{
				// 公開鍵は全ユーザー読み取り可。write 属性は残して再インポート時に上書き可能にする。
				file.SetReadable(true, false);
				Settings.EngineSettings.SshPublicKeyPath = destPath;
			}
			else
			{
				// 秘密鍵は owner のみ読み取り可（SSH クライアントの必須条件）。
				// .NET の ReadOnly 扱いになるのを避けるため write 属性は owner 維持のまま残す。
				file.SetReadable(false, false);
				file.SetReadable(true, true);
				Settings.EngineSettings.VastAiSshKeyPath = destPath;
			}
			Settings.Save();

			Toast.MakeText(this,
				isPublic ? "SSH公開鍵をインポートしました" : "SSH秘密鍵をインポートしました",
				ToastLength.Short).Show();

			if (FragmentManager.FindFragmentById(16908290) is SettingsFragment fragment)
			{
				fragment.RefreshSshKeySummaries();
			}
		}
		catch (Exception ex)
		{
			Toast.MakeText(this, $"SSH鍵の読み込みに失敗: {ex.Message}", ToastLength.Long).Show();
		}
	}

	private string ResolveFileNameFromUri(Android.Net.Uri uri)
	{
		string name = null;
		if (uri.Scheme == "content")
		{
			using var cursor = ContentResolver.Query(uri, null, null, null, null);
			if (cursor != null && cursor.MoveToFirst())
			{
				int idx = cursor.GetColumnIndex(Android.Provider.OpenableColumns.DisplayName);
				if (idx >= 0) name = cursor.GetString(idx);
			}
		}
		if (string.IsNullOrEmpty(name))
			name = IOPath.GetFileName(uri.Path);
		return name;
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
