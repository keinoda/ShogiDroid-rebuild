using System.IO;
using ShogiLib;

namespace ShogiGUI.Engine;

public class RemoteEnginePlayer : EnginePlayer
{
	public const int RemoteEngineNo = -1;

	private string host_;

	private int port_;

	// SSH接続用
	private int sshPort_;
	private string sshKeyPath_;
	private string engineCommand_;

	public bool UseSsh => !string.IsNullOrEmpty(sshKeyPath_) && sshPort_ > 0;

	private string SettingsFolder => Path.Combine(EngineFile.EngineFolder, "remote_engine");

	public RemoteEnginePlayer(PlayerColor color, string host, int port)
		: base(color)
	{
		host_ = host;
		port_ = port;
	}

	public RemoteEnginePlayer(PlayerColor color, string host, int sshPort, string sshKeyPath, string engineCommand)
		: base(color)
	{
		host_ = host;
		sshPort_ = sshPort;
		sshKeyPath_ = sshKeyPath;
		engineCommand_ = engineCommand;
	}

	public bool InitSsh()
	{
		return InitRemoteSsh(host_, sshPort_, sshKeyPath_, engineCommand_);
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
