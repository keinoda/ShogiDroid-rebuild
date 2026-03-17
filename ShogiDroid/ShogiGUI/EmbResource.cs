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
		bool result = false;
		if (Application.Context.Assets.List(path).Length != 0)
		{
			result = true;
		}
		else
		{
			try
			{
				using Stream stream = Application.Context.Assets.Open(path);
				stream.Close();
			}
			catch (Java.IO.IOException)
			{
				result = true;
			}
		}
		return result;
	}

	public static string[] GetFiles(string path)
	{
		return Application.Context.Assets.List(path);
	}
}
