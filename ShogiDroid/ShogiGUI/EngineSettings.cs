using System.IO;

namespace ShogiGUI;

public class EngineSettings
{
	public int Time = 300000;

	public int Countdown = 10000;

	public int EngineNo = 1;

	public string EngineName = string.Empty;

	public string EngineFolder = string.Empty;

	public string RemoteHost = string.Empty;

	public string RemotePort = "28597";

	public string GetExternalEngineFolder()
	{
		if (!(EngineFolder == string.Empty))
		{
			return EngineFolder;
		}
		return LocalFile.EnginePath;
	}

	public string GetExternalEngineFile()
	{
		return Path.Combine((EngineFolder == string.Empty) ? LocalFile.EnginePath : EngineFolder, EngineName);
	}
}
