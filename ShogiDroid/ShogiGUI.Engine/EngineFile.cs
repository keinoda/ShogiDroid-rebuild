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
		bool result = false;
		if (!Directory.Exists(path))
		{
			try
			{
				Directory.CreateDirectory(path);
			}
			catch
			{
				result = true;
			}
		}
		return result;
	}

	private static bool CopyFileFromResource(string dest, string src)
	{
		bool result = false;
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
		}
		catch (Java.IO.IOException)
		{
			result = true;
		}
		return result;
	}

	public static bool CopyFile(string dest, string src)
	{
		bool result = false;
		try
		{
			System.IO.File.Copy(src, dest, overwrite: true);
		}
		catch
		{
			result = true;
		}
		return result;
	}

	public static string FindEngine(string dir)
	{
		string[] files = Directory.GetFiles(dir);
		List<string> list = new List<string>();
		list.Add(Build.CpuAbi);
		if (Build.CpuAbi2 != string.Empty)
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
		string text = string.Empty;
		foreach (string ext in list)
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
		bool result = false;
		try
		{
			byte[] array = new byte[4];
			byte[] array2 = new byte[4];
			using Stream stream = EmbResource.Open(src);
			using Stream stream2 = System.IO.File.Open(dest, FileMode.Open);
			stream.Read(array, 0, 4);
			stream2.Read(array2, 0, 4);
			result = true;
			for (int i = 0; i < 4; i++)
			{
				if (array[i] != array2[i])
				{
					result = false;
					break;
				}
			}
		}
		catch
		{
		}
		return result;
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
