using System.IO;

namespace ShogiGUI;

public class EngineSettings
{
	public int Time = 300000;

	public int Countdown = 10000;

	public int Increment = 0;

	public int EngineNo = 1;

	public string EngineName = string.Empty;

	public string EngineFolder = string.Empty;

	public string RemoteHost = string.Empty;

	public string RemotePort = "28597";

	// vast.ai settings
	public string VastAiApiKey = string.Empty;

	public string VastAiDockerImage = "keinoda/shogi:v9.0";

	public string VastAiOnStartCmd = "env >> /etc/environment; touch ~/.no_auto_tmux;";

	// SSH接続設定
	public string VastAiSshKeyPath = string.Empty;
	public int VastAiSshPort = 0;
	public string VastAiSshEngineCommand = string.Empty;

	// インスタンススペック（自動オプション設定用）
	public int VastAiCpuCores = 0;
	public int VastAiRamMb = 0;
	public int VastAiGpuRamMb = 0;

	public int VastAiInstanceId = 0;

	// vast.ai search criteria
	public string VastAiGpuNames = "RTX 4090, RTX 5090";
	public int VastAiMinCpuCores = 32;
	public double VastAiMaxDph = 0.5;
	public int VastAiMinGpuRam = 0;
	public int VastAiMinDiskSpace = 0;
	public double VastAiMinReliability = 0;
	public double VastAiMinInetDown = 0;
	public int VastAiNumGpus = 0;
	public string VastAiSortField = "dph_total";
	public bool VastAiSortAsc = true;
	public double VastAiMinCudaVersion = 0;

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
