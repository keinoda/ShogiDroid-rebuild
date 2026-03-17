using System.IO;
using Android.OS;
using ShogiLib;

namespace ShogiGUI.Engine;

public class InternalEnginePlayer : EnginePlayer
{
	public static readonly string EngineBaseName = "gpsfish";

	private readonly string srcFolder = EngineBaseName;

	private readonly string srcDataFolder = EngineBaseName + "/data";

	private readonly string srcVer = EngineBaseName + "/" + EngineBaseName + ".ver";

	private string EngineName
	{
		get
		{
			string text = EngineBaseName + "-";
			if (Build.CpuAbi == "x86" || Build.CpuAbi == "x86_64")
			{
				return text + "x86";
			}
			if (Build.CpuAbi == "armeabi-v7a" || Build.CpuAbi2 == "armeabi-v7a")
			{
				return text + "armeabi-v7a";
			}
			return text + "armeabi";
		}
	}

	private string EngineFolder => Path.Combine(EngineFile.EngineFolder, EngineBaseName);

	public virtual string EnginePath => Path.Combine(EngineFolder, EngineBaseName);

	public override string WorkingDirectory => EngineFolder;

	public InternalEnginePlayer(PlayerColor color)
		: base(color)
	{
	}

	public override bool CopyFiles()
	{
		string engineName = EngineName;
		string enginePath = EnginePath;
		if (!EngineFile.Compare(enginePath + ".ver", srcVer))
		{
			if (Directory.Exists(EngineFolder))
			{
				Directory.Delete(EngineFolder, recursive: true);
			}
			Directory.CreateDirectory(EngineFolder);
			EngineFile.CopyFilesFromResource(enginePath, Path.Combine(srcFolder, engineName));
			EngineFile.Chmod(enginePath, 484);
			EngineFile.CopyFilesFromResource(enginePath + ".ver", srcVer);
			EngineFile.CopyFilesFromResource(Path.Combine(EngineFolder, Path.GetFileName(srcDataFolder)), srcDataFolder);
		}
		return true;
	}

	public override void LoadSettings()
	{
		string filename = Path.Combine(EngineFolder, EngineBaseName) + ".xml";
		engineOptions_ = EngineOptions.Load(filename);
	}

	public override void SaveSettings()
	{
		EngineOptions.Save(Path.Combine(EngineFolder, EngineBaseName) + ".xml", engineOptions_);
	}
}
