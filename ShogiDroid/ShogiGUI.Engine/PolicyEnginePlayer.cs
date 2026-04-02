using System;
using System.Collections.Generic;
using System.IO;
using Android.OS;
using ShogiGUI;
using ShogiLib;

namespace ShogiGUI.Engine;

public class PolicyEnginePlayer : EnginePlayer
{
	public const string EngineBaseName = "policy_engine";

	private const string AssetFolder = "policy-engine";

	private const string AssetVersion = "policy-v1.0.0";

	private string EngineFolder => Path.Combine(EngineFile.EngineFolder, "policy");

	public override string WorkingDirectory => EngineFolder;

	public string EnginePath => Path.Combine(EngineFolder, EngineBaseName);

	public PolicyEnginePlayer(PlayerColor color)
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

		// エンジンバイナリ
		string assetBinary = FindAssetBinary();
		if (assetBinary == string.Empty)
		{
			AppDebug.Log.Error("PolicyEnginePlayer: 対応するバイナリが見つかりません");
			return false;
		}
		if (EngineFile.CopyFilesFromResource(enginePath, assetBinary))
		{
			AppDebug.Log.Error($"PolicyEnginePlayer: バイナリコピー失敗 {assetBinary}");
			return false;
		}
		_ = EngineFile.Chmod(enginePath, 484);

		// model.onnx
		string modelSrc = Path.Combine(AssetFolder, "model.onnx");
		string modelDst = Path.Combine(EngineFolder, "model.onnx");
		if (EngineFile.CopyFilesFromResource(modelDst, modelSrc))
		{
			AppDebug.Log.Error("PolicyEnginePlayer: model.onnx コピー失敗");
			return false;
		}

		// libonnxruntime.so（Assets では .bin としてリネーム、コピー時に .so に戻す）
		string soSrc = Path.Combine(AssetFolder, "libonnxruntime.bin");
		string soDst = Path.Combine(EngineFolder, "libonnxruntime.so");
		if (EngineFile.CopyFilesFromResource(soDst, soSrc))
		{
			AppDebug.Log.Error("PolicyEnginePlayer: libonnxruntime.so コピー失敗");
			return false;
		}
		_ = EngineFile.Chmod(soDst, 484);

		System.IO.File.WriteAllText(versionPath, AssetVersion);
		AppDebug.Log.Info("PolicyEnginePlayer: ファイル展開完了");
		return true;
	}

	public override void LoadSettings()
	{
		tempOptions_["MultiPV"] = "7";
		tempOptions_["Softmax_Temperature"] = "100";
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
			list.Add(Build.CpuAbi);
		if (!string.IsNullOrEmpty(Build.CpuAbi2))
			list.Add(Build.CpuAbi2);
		if (Build.CpuAbi == "arm64-v8a")
		{
			list.Add("armeabi-v7a");
			list.Add("armeabi");
		}
		return list;
	}
}
