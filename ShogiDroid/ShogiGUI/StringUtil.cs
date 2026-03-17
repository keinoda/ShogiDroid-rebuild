using System.IO;
using System.Text;
using Hnx8.ReadJEnc;

namespace ShogiGUI;

public static class StringUtil
{
	public static string Load(string filename)
	{
		return Load(filename, Encoding.GetEncoding(932));
	}

	public static string Load(string filename, Encoding encoding)
	{
		string empty = string.Empty;
		using StreamReader streamReader = new StreamReader(filename, encoding);
		empty = streamReader.ReadToEnd();
		streamReader.Close();
		return empty;
	}

	public static string Load(Stream stream)
	{
		Encoding encoding = Encoding.GetEncoding(932);
		byte[] array = new byte[1024];
		int len = stream.Read(array, 0, array.Length);
		string text;
		CharCode encoding2 = ReadJEnc.JP.GetEncoding(array, len, out text);
		if (encoding2 != null)
		{
			encoding = encoding2.GetEncoding();
		}
		return text + Load(stream, encoding);
	}

	public static string Load(Stream stream, Encoding encoding)
	{
		string empty = string.Empty;
		using StreamReader streamReader = new StreamReader(stream, encoding);
		empty = streamReader.ReadToEnd();
		streamReader.Close();
		return empty;
	}

	public static string ReplaceInvalidFileNameChars(this string str)
	{
		string text = str;
		Path.GetInvalidFileNameChars();
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		return text;
	}

	public static Encoding GetEncording(string filename)
	{
		Encoding encoding = Encoding.GetEncoding(932);
		try
		{
			using FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
			byte[] array = new byte[1024];
			int len = fileStream.Read(array, 0, array.Length);
			string text;
			CharCode encoding2 = ReadJEnc.JP.GetEncoding(array, len, out text);
			if (encoding2 != null)
			{
				encoding = encoding2.GetEncoding();
			}
		}
		catch
		{
		}
		return encoding;
	}
}
