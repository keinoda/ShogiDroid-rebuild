using System;
using System.IO;
using System.Xml.Serialization;
using Android.App;
using Android.Preferences;

namespace ShogiGUI;

public sealed class Settings
{
	[XmlElement("AppSettings")]
	public AppSettings App = new AppSettings();

	[XmlElement("EngineSettings")]
	public EngineSettings Engine = new EngineSettings();

	[XmlElement("AnalyzeSettings")]
	public AnalyzeSettings Analyze = new AnalyzeSettings();

	private static Settings settings = new Settings();

	public const string BackupFileName = "shogidroid-settings.xml";

	public static AppSettings AppSettings => settings.App;

	public static EngineSettings EngineSettings => settings.Engine;

	public static AnalyzeSettings AnalyzeSettings => settings.Analyze;

	public static void Load()
	{
		try
		{
			new PrefSerializer().Deserialize(PreferenceManager.GetDefaultSharedPreferences(Application.Context), settings);
			MigrateOnStartCmd();
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"Settings.Load に失敗: {ex.Message}");
		}
	}

	/// <summary>
	/// OnStartCmdを常に最新のデフォルト値に強制上書き
	/// </summary>
	private static void MigrateOnStartCmd()
	{
		string defaultCmd = new EngineSettings().VastAiOnStartCmd;
		if (settings.Engine.VastAiOnStartCmd != defaultCmd)
		{
			settings.Engine.VastAiOnStartCmd = defaultCmd;
			Save();
		}
	}

	public static void Save()
	{
		Save(clearExisting: false);
	}

	private static void Save(bool clearExisting)
	{
		try
		{
			var pref = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
			if (clearExisting)
			{
				var editor = pref.Edit();
				editor.Clear();
				editor.Commit();
			}
			new PrefSerializer().Serialize(pref, settings);
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"Settings.Save に失敗: {ex.Message}");
		}
	}

	public static void Reset()
	{
		settings = new Settings();
	}

	public static string GetBackupFilePath()
	{
		return Path.Combine(LocalFile.SettingsPath, BackupFileName);
	}

	public static bool ExportToFile(string path, out string errorMessage)
	{
		errorMessage = string.Empty;
		try
		{
			Load();
			string directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
			var serializer = new XmlSerializer(typeof(Settings));
			using var stream = File.Create(path);
			serializer.Serialize(stream, settings);
			LocalFile.ScanFile(path);
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			return false;
		}
	}

	public static bool ImportFromFile(string path, out string errorMessage)
	{
		errorMessage = string.Empty;
		try
		{
			var serializer = new XmlSerializer(typeof(Settings));
			using var stream = File.OpenRead(path);
			if (serializer.Deserialize(stream) is not Settings imported)
			{
				errorMessage = "設定ファイルを読み込めませんでした";
				return false;
			}
			settings = imported;
			Save(clearExisting: true);
			MigrateOnStartCmd();
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			return false;
		}
	}
}
