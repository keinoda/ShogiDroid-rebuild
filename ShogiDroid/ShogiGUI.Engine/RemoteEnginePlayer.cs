using System.IO;
using ShogiLib;

namespace ShogiGUI.Engine;

public class RemoteEnginePlayer : EnginePlayer
{
	public const int RemoteEngineNo = -1;

	private string host_;

	private int port_;

	private string SettingsFolder => Path.Combine(EngineFile.EngineFolder, "remote_engine");

	public RemoteEnginePlayer(PlayerColor color, string host, int port)
		: base(color)
	{
		host_ = host;
		port_ = port;
	}

	public override bool CopyFiles()
	{
		Directory.CreateDirectory(SettingsFolder);
		return true;
	}

	public override void LoadSettings()
	{
		string filename = Path.Combine(SettingsFolder, "remote_engine.xml");
		engineOptions_ = EngineOptions.Load(filename);
	}

	public override void SaveSettings()
	{
		string filename = Path.Combine(SettingsFolder, "remote_engine.xml");
		EngineOptions.Save(filename, engineOptions_);
	}
}
