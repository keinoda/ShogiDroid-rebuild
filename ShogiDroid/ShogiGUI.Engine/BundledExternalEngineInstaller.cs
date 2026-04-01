using System.IO;
using ShogiGUI;

namespace ShogiGUI.Engine;

public static class BundledExternalEngineInstaller
{
	private static readonly string[] EngineBaseNames = { "Suisho11beta" };

	public static void InstallAll()
	{
		RemoveInternalEngineFolders();

		foreach (string engineBaseName in EngineBaseNames)
		{
			Install(engineBaseName);
		}
	}

	private static void RemoveInternalEngineFolders()
	{
		foreach (string engineBaseName in InternalEngineCatalog.EngineBaseNames)
		{
			string installFolder = Path.Combine(LocalFile.EnginePath, engineBaseName);
			if (Directory.Exists(installFolder))
			{
				Directory.Delete(installFolder, recursive: true);
			}
		}
	}

	private static void Install(string engineBaseName)
	{
		string assetFolder = engineBaseName;
		string versionAsset = Path.Combine(assetFolder, engineBaseName + ".ver");
		if (!EngineFile.AssetExists(versionAsset))
		{
			return;
		}

		string installFolder = Path.Combine(LocalFile.EnginePath, engineBaseName);
		string versionPath = Path.Combine(installFolder, engineBaseName + ".ver");
		if (EngineFile.Compare(versionPath, versionAsset))
		{
			return;
		}

		string assetBinary = EngineFile.FindAssetBinary(assetFolder);
		if (assetBinary == string.Empty)
		{
			AppDebug.Log.Error($"BundledExternalEngineInstaller: compatible asset binary not found for {engineBaseName}");
			return;
		}

		string evalAsset = Path.Combine(assetFolder, "eval");
		bool bundlesEvalFile = EngineFile.AssetExists(Path.Combine(evalAsset, "nn.bin"));

		if (Directory.Exists(installFolder))
		{
			if (bundlesEvalFile)
			{
				Directory.Delete(installFolder, recursive: true);
				Directory.CreateDirectory(installFolder);
			}
			else
			{
				// Preserve user-supplied eval files such as Suisho11beta's nn.bin.
				foreach (string file in Directory.GetFiles(installFolder))
				{
					File.Delete(file);
				}
			}
		}
		else
		{
			Directory.CreateDirectory(installFolder);
		}

		string installedBinary = Path.Combine(installFolder, Path.GetFileName(assetBinary));
		if (EngineFile.CopyFilesFromResource(installedBinary, assetBinary))
		{
			AppDebug.Log.Error($"BundledExternalEngineInstaller: failed to copy asset {assetBinary}");
			return;
		}
		_ = EngineFile.Chmod(installedBinary, 484);

		if (EngineFile.AssetDirectoryExists(evalAsset) && EngineFile.CopyFilesFromResource(Path.Combine(installFolder, "eval"), evalAsset))
		{
			AppDebug.Log.Error($"BundledExternalEngineInstaller: failed to copy asset {evalAsset}");
			return;
		}
		if (EngineFile.CopyFilesFromResource(versionPath, versionAsset))
		{
			AppDebug.Log.Error($"BundledExternalEngineInstaller: failed to copy version asset {versionAsset}");
			return;
		}

		LocalFile.ScanFile(installFolder);
	}
}
