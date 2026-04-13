using System;
using System.IO;
using Android.App;
using Android.Media;
using Android.OS;
using ShogiGUI.Engine;

namespace ShogiGUI;

public class LocalFile
{
	public static string PersonalFolderPath => System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

	public static string StorageFolderPath => Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "ShogiDroid");

	public static string KifPath => Path.Combine(StorageFolderPath, "kif");

	public static string EnginePath => Path.Combine(StorageFolderPath, "engine");

	public static string BookPath => Path.Combine(StorageFolderPath, "book");

	public static string SettingsPath => Path.Combine(StorageFolderPath, "settings");

	public static void CreateFolders()
	{
		string storageFolderPath = StorageFolderPath;
		try
		{
			Directory.CreateDirectory(KifPath);
			Directory.CreateDirectory(EnginePath);
			Directory.CreateDirectory(Path.Combine(storageFolderPath, "book"));
			Directory.CreateDirectory(SettingsPath);
			BundledExternalEngineInstaller.InstallAll();
			MediaScannerConnection.ScanFile(Application.Context, new string[1] { storageFolderPath }, null, new MediaScannerClient());
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"LocalFile.CreateFolders に失敗: {ex.Message}");
		}
	}

	public static void ScanFile(string filename)
	{
		MediaScannerConnection.ScanFile(Application.Context, new string[1] { filename }, null, new MediaScannerClient());
	}

	public static bool FileExist(string path, string filename)
	{
		return File.Exists(Path.Combine(path, filename));
	}
}
