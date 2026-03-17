using System;
using System.IO;
using ShogiLib;

namespace ShogiGUI.Engine;

public class ExternalEnginePlayer : EnginePlayer
{
	private string engineFileName;

	private string baseName;

	private string working_dir;

	private string EngineFolder => Path.Combine(EngineFile.EngineFolder, baseName);

	public virtual string EnginePath => Path.Combine(EngineFolder, baseName);

	public override string WorkingDirectory => working_dir;

	public ExternalEnginePlayer(PlayerColor color, string filename)
		: base(color)
	{
		engineFileName = filename;
		baseName = Path.GetFileNameWithoutExtension(filename);
	}

	public override bool CopyFiles()
	{
		bool result = true;
		string enginePath = EnginePath;
		AppDebug.Log.Info($"ExternalEngine.CopyFiles: engineFileName={engineFileName}, enginePath={enginePath}");
		string text;
		if (Directory.Exists(engineFileName))
		{
			text = EngineFile.FindEngine(engineFileName);
			working_dir = engineFileName;
			AppDebug.Log.Info($"ExternalEngine.CopyFiles: found binary={text}, working_dir={working_dir}");
		}
		else
		{
			text = engineFileName;
			working_dir = Path.GetDirectoryName(text);
			AppDebug.Log.Info($"ExternalEngine.CopyFiles: using file directly={text}");
		}
		if (text == string.Empty)
		{
			AppDebug.Log.Error("ExternalEngine.CopyFiles: no engine binary found");
			result = false;
		}
		else
		{
			try
			{
				Directory.CreateDirectory(EngineFolder);
				EngineFile.CopyFile(enginePath, text);
				int chmodResult = EngineFile.Chmod(enginePath, 484);
				AppDebug.Log.Info($"ExternalEngine.CopyFiles: copied to {enginePath}, chmod result={chmodResult}");
			}
			catch (Exception e)
			{
				AppDebug.Log.ErrorException(e, "ExternalEngine.CopyFiles failed");
				result = false;
			}
		}
		return result;
	}

	public override void LoadSettings()
	{
		string filename = Path.Combine(EngineFolder, baseName) + ".xml";
		engineOptions_ = EngineOptions.Load(filename);
	}

	public override void SaveSettings()
	{
		EngineOptions.Save(Path.Combine(EngineFolder, baseName) + ".xml", engineOptions_);
	}

	public void Uninstall()
	{
		if (Directory.Exists(EngineFolder))
		{
			Directory.Delete(EngineFolder, recursive: true);
		}
	}
}
