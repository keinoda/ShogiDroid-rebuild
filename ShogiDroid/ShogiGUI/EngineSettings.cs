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

	/// <summary>
	/// release ブランチでは "keinoda/shogi:AobaNNUE" に変更する。
	/// SSH鍵フィンガープリントが一致しない場合はUI上で変更不可。
	/// </summary>
	public string VastAiDockerImage = "keinoda/shogi:v9.21nnue";

	/// <summary>
	/// Dockerイメージのロック用。空文字ならロックしない（develop用）。
	/// release ブランチではフィンガープリントを設定する。
	/// </summary>
	public static string DockerImageLockFingerprint = string.Empty;

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

	// AWS スポットインスタンス設定
	public string AwsAccessKey = string.Empty;
	public string AwsSecretKey = string.Empty;
	public string AwsRegion = "eu-north-1";
	public string AwsAvailabilityZone = string.Empty;
	public string AwsInstanceType = "c7a.metal-48xl";
	public string AwsDockerImage = "keinoda/shogi:v9.21nnue";
	public string AwsInstanceId = string.Empty;
	public string AwsKeyPairName = string.Empty;
	public string AwsSecurityGroupId = string.Empty;
	public string AwsVolumeId = string.Empty;
	public string AwsCustomAmiId = string.Empty;

	/// <summary>
	/// 現在どのクラウドプロバイダーで接続中か ("vastai" or "aws")
	/// </summary>
	public string CloudProvider = string.Empty;

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
