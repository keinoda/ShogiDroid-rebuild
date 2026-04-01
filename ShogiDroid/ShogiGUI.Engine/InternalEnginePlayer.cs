using System.IO;
using ShogiLib;

namespace ShogiGUI.Engine;

public class InternalEnginePlayer : EnginePlayer
{
	private readonly string engineBaseName;

	private string SrcFolder => engineBaseName;

	private string SrcDataFolder => engineBaseName + "/eval";

	private string SrcVer => engineBaseName + "/" + engineBaseName + ".ver";

	private string EngineFolder => Path.Combine(EngineFile.EngineFolder, engineBaseName);

	public virtual string EnginePath => Path.Combine(EngineFolder, engineBaseName);

	public override string WorkingDirectory => EngineFolder;

	public string EngineBaseName => engineBaseName;

	public InternalEnginePlayer(PlayerColor color, string engineBaseName)
		: base(color)
	{
		this.engineBaseName = engineBaseName;
	}

	public override bool CopyFiles()
	{
		string enginePath = EnginePath;
		if (EngineFile.Compare(enginePath + ".ver", SrcVer))
		{
			return true;
		}

		string assetBinary = EngineFile.FindAssetBinary(SrcFolder);
		if (assetBinary == string.Empty)
		{
			AppDebug.Log.Error("InternalEnginePlayer: compatible asset binary not found");
			return false;
		}

		if (Directory.Exists(EngineFolder))
		{
			Directory.Delete(EngineFolder, recursive: true);
		}
		Directory.CreateDirectory(EngineFolder);

		if (EngineFile.CopyFilesFromResource(enginePath, assetBinary))
		{
			AppDebug.Log.Error($"InternalEnginePlayer: failed to copy asset {assetBinary}");
			return false;
		}
		_ = EngineFile.Chmod(enginePath, 484);
		if (EngineFile.CopyFilesFromResource(enginePath + ".ver", SrcVer))
		{
			AppDebug.Log.Error($"InternalEnginePlayer: failed to copy version asset {SrcVer}");
			return false;
		}
		if (EngineFile.CopyFilesFromResource(Path.Combine(EngineFolder, Path.GetFileName(SrcDataFolder)), SrcDataFolder))
		{
			AppDebug.Log.Error($"InternalEnginePlayer: failed to copy data asset {SrcDataFolder}");
			return false;
		}
		return true;
	}

	public override void LoadSettings()
	{
		string filename = Path.Combine(EngineFolder, engineBaseName) + ".xml";
		engineOptions_ = EngineOptions.Load(filename);
	}

	public override void SaveSettings()
	{
		EngineOptions.Save(Path.Combine(EngineFolder, engineBaseName) + ".xml", engineOptions_);
	}
}
