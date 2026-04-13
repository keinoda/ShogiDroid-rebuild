using System.IO;
using Android.App;
using Java.IO;

namespace ShogiGUI;

public class EmbResource
{
	public static string RootPath => string.Empty;

	public static Stream Open(string filename)
	{
		return Application.Context.Assets.Open(filename);
	}

	public static bool IsDirectory(string path)
	{
		if (Application.Context.Assets.List(path).Length != 0)
		{
			return true;
		}
		try
		{
			using Stream stream = Application.Context.Assets.Open(path);
			stream.Close();
			return false;
		}
		catch (Java.IO.IOException)
		{
			return true;
		}
	}

	public static string[] GetFiles(string path)
	{
		return Application.Context.Assets.List(path);
	}
}
