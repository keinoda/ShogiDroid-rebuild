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

	public static AppSettings AppSettings => settings.App;

	public static EngineSettings EngineSettings => settings.Engine;

	public static AnalyzeSettings AnalyzeSettings => settings.Analyze;

	public static void Load()
	{
		try
		{
			new PrefSerializer().Deserialize(PreferenceManager.GetDefaultSharedPreferences(Application.Context), settings);
		}
		catch
		{
		}
	}

	public static void Save()
	{
		try
		{
			new PrefSerializer().Serialize(PreferenceManager.GetDefaultSharedPreferences(Application.Context), settings);
		}
		catch
		{
		}
	}

	public static void Reset()
	{
		settings = new Settings();
	}
}
