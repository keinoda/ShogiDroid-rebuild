using System;
using System.Collections.Generic;
using System.IO;
using Android.OS;
using Java.IO;

namespace ShogiGUI.Engine;

public class EngineFile
{
	public static string EngineFolder => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "engine");

	public static bool CopyFilesFromResource(string dest, string src)
	{
		bool flag = false;
		if (EmbResource.IsDirectory(src))
		{
			MakeDir(dest);
			string[] files = EmbResource.GetFiles(src);
			foreach (string path in files)
			{
				flag = CopyFilesFromResource(Path.Combine(dest, path), Path.Combine(src, path));
				if (flag)
				{
					break;
				}
			}
		}
		else
		{
			flag = CopyFileFromResource(dest, src);
		}
		return flag;
	}

	private static bool CopyFiles(string dest, string src)
	{
		bool flag = false;
		if (Directory.Exists(src))
		{
			MakeDir(dest);
			string[] files = Directory.GetFiles(src);
			foreach (string path in files)
			{
				flag = CopyFiles(Path.Combine(dest, path), Path.Combine(src, path));
				if (flag)
				{
					break;
				}
			}
		}
		else if (System.IO.File.Exists(src))
		{
			CopyFile(dest, src);
		}
		return flag;
	}

	private static bool MakeDir(string path)
	{
		if (!Directory.Exists(path))
		{
			try
			{
				Directory.CreateDirectory(path);
			}
			catch
			{
				return true;
			}
		}
		return false;
	}

	public static bool AssetExists(string path)
	{
		try
		{
			string normalized = path.Replace('\\', '/');
			string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
			string fileName = Path.GetFileName(normalized);
			return Array.Exists(EmbResource.GetFiles(directory), (string file) => file == fileName);
		}
		catch
		{
			return false;
		}
	}

	public static bool AssetDirectoryExists(string path)
	{
		try
		{
			return EmbResource.GetFiles(path).Length != 0;
		}
		catch
		{
			return false;
		}
	}

	public static List<string> GetPreferredAbis()
	{
		List<string> list = new List<string>();
		AddAbi(list, Build.CpuAbi);
		AddAbi(list, Build.CpuAbi2);
		if (Build.CpuAbi == "x86_64")
		{
			AddAbi(list, "x86");
		}
		else if (Build.CpuAbi == "arm64-v8a")
		{
			AddAbi(list, "armeabi-v7a");
			AddAbi(list, "armeabi");
		}
		else
		{
			AddAbi(list, "armeabi");
		}
		return list;
	}

	public static string FindAssetBinary(string assetFolder)
	{
		string[] files = EmbResource.GetFiles(assetFolder);
		foreach (string abi in GetPreferredAbis())
		{
			foreach (string file in files)
			{
				if (Path.GetFileNameWithoutExtension(file).EndsWith(abi, StringComparison.OrdinalIgnoreCase))
				{
					return Path.Combine(assetFolder, file);
				}
			}
		}
		return string.Empty;
	}

	private static bool CopyFileFromResource(string dest, string src)
	{
		string path = dest;
		if (Directory.Exists(dest))
		{
			path = Path.Combine(dest, Path.GetFileName(src));
		}
		try
		{
			using Stream stream = EmbResource.Open(src);
			using Stream destination = System.IO.File.Open(path, FileMode.Create);
			stream.CopyTo(destination);
			return false;
		}
		catch (Java.IO.IOException)
		{
			return true;
		}
	}

	public static bool CopyFile(string dest, string src)
	{
		try
		{
			System.IO.File.Copy(src, dest, overwrite: true);
			return false;
		}
		catch
		{
			return true;
		}
	}

	public static string FindEngine(string dir)
	{
		string[] files = Directory.GetFiles(dir);
		string text = string.Empty;
		foreach (string ext in GetPreferredAbis())
		{
			int num = Array.FindIndex(files, (string file) => Path.GetFileNameWithoutExtension(file).EndsWith(ext));
			if (num >= 0)
			{
				text = files[num];
				break;
			}
		}
		if (text == string.Empty)
		{
			int num2 = Array.FindIndex(files, (string file) => Path.GetExtension(file) == ".exe");
			if (num2 >= 0)
			{
				text = files[num2];
			}
		}
		return text;
	}

	public static bool Compare(string dest, string src)
	{
		try
		{
			if (!AssetExists(src) || !System.IO.File.Exists(dest))
			{
				return false;
			}
			using Stream stream = EmbResource.Open(src);
			using StreamReader reader = new StreamReader(stream);
			return reader.ReadToEnd().Trim() == System.IO.File.ReadAllText(dest).Trim();
		}
		catch
		{
			return false;
		}
	}

	private static void AddAbi(List<string> list, string abi)
	{
		if (!string.IsNullOrEmpty(abi) && !list.Contains(abi))
		{
			list.Add(abi);
		}
	}

	public static int Chmod(string path, int mode)
	{
		try
		{
			string modeStr = Convert.ToString(mode, 8);
			var process = Java.Lang.Runtime.GetRuntime().Exec(new string[] { "chmod", modeStr, path });
			process.WaitFor();
			return process.ExitValue();
		}
		catch (Exception e)
		{
			AppDebug.Log.ErrorException(e, "Chmod failed for " + path);
			return -1;
		}
	}
}
