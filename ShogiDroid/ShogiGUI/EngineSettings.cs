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

	public string VastAiOnStartCmd = "env >> /etc/environment; touch ~/.no_auto_tmux; setsid bash -c 'cd /workspace/Suisho10 && socat TCP-LISTEN:6000,reuseaddr,bind=0.0.0.0,fork,keepalive,tcp-keepidle=60,tcp-keepintvl=10,tcp-keepcnt=3 EXEC:./Suisho10-YaneuraOu-tournament-avx2,pty,raw,echo=0' & setsid bash -c 'cd /workspace/FukauraOu && socat TCP-LISTEN:6001,reuseaddr,bind=0.0.0.0,fork,keepalive,tcp-keepidle=60,tcp-keepintvl=10,tcp-keepcnt=3 EXEC:./FukauraOu-avx2,pty,raw,echo=0' & setsid bash -c 'cd /workspace/FukauraOu && socat TCP-LISTEN:6002,reuseaddr,bind=0.0.0.0,fork,keepalive,tcp-keepidle=60,tcp-keepintvl=10,tcp-keepcnt=3 EXEC:bash' &;";

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
