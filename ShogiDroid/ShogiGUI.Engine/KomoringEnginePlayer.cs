using System;
using System.Collections.Generic;
using System.IO;
using Android.OS;
using ShogiGUI;
using ShogiLib;

namespace ShogiGUI.Engine;

public class KomoringEnginePlayer : EnginePlayer
{
	public const string EngineBaseName = "KomoringHeights";

	private const string AssetFolder = "KomoringHeights-kh-v1.1.0-android";

	private const string AssetVersion = "kh-v1.1.0-android";

	private string EngineFolder => Path.Combine(EngineFile.EngineFolder, "komoring");

	public override string WorkingDirectory => EngineFolder;

	public string EnginePath => Path.Combine(EngineFolder, EngineBaseName);

	public KomoringEnginePlayer(PlayerColor color)
		: base(color)
	{
	}

	public override bool CopyFiles()
	{
		string enginePath = EnginePath;
		string versionPath = enginePath + ".ver";
		if (!NeedsCopy(enginePath, versionPath))
		{
			return true;
		}

		if (Directory.Exists(EngineFolder))
		{
			Directory.Delete(EngineFolder, recursive: true);
		}
		Directory.CreateDirectory(EngineFolder);

		string assetBinary = FindAssetBinary();
		if (assetBinary == string.Empty)
		{
			AppDebug.Log.Error("KomoringEnginePlayer: compatible asset binary not found");
			return false;
		}
		if (EngineFile.CopyFilesFromResource(enginePath, assetBinary))
		{
			AppDebug.Log.Error($"KomoringEnginePlayer: failed to copy asset {assetBinary}");
			return false;
		}
		_ = EngineFile.Chmod(enginePath, 484);
		System.IO.File.WriteAllText(versionPath, AssetVersion);
		return true;
	}

	public override void LoadSettings()
	{
		tempOptions_["Threads"] = "1";
		tempOptions_["USI_Hash"] = "128";
		tempOptions_["MultiPV"] = "1";
		tempOptions_["GenerateAllLegalMoves"] = "true";
		tempOptions_["RootIsAndNodeIfChecked"] = "false";
		tempOptions_["PvInterval"] = "1000";
	}

	private static bool NeedsCopy(string enginePath, string versionPath)
	{
		if (!System.IO.File.Exists(enginePath) || !System.IO.File.Exists(versionPath))
		{
			return true;
		}
		try
		{
			return System.IO.File.ReadAllText(versionPath).Trim() != AssetVersion;
		}
		catch
		{
			return true;
		}
	}

	private static string FindAssetBinary()
	{
		string[] files = EmbResource.GetFiles(AssetFolder);
		foreach (string abi in GetPreferredAbis())
		{
			foreach (string file in files)
			{
				if (Path.GetFileNameWithoutExtension(file).EndsWith(abi, StringComparison.OrdinalIgnoreCase))
				{
					return Path.Combine(AssetFolder, file);
				}
			}
		}
		return string.Empty;
	}

	private static IEnumerable<string> GetPreferredAbis()
	{
		var list = new List<string>();
		if (!string.IsNullOrEmpty(Build.CpuAbi))
		{
			list.Add(Build.CpuAbi);
		}
		if (!string.IsNullOrEmpty(Build.CpuAbi2))
		{
			list.Add(Build.CpuAbi2);
		}
		if (Build.CpuAbi == "x86_64")
		{
			list.Add("x86");
		}
		else if (Build.CpuAbi == "arm64-v8a")
		{
			list.Add("armeabi-v7a");
			list.Add("armeabi");
		}
		else
		{
			list.Add("armeabi");
		}
		return list;
	}
}
