using System;
using System.IO;

namespace Hnx8.ReadJEnc;

public class FileReader : IDisposable
{
	public ReadJEnc ReadJEnc = ReadJEnc.JP;

	protected byte[] Bytes;

	protected int Length;

	protected string text;

	public string Text => text;

	public FileReader(FileInfo file)
		: this((int)((file.Length < 2147483647) ? file.Length : 0))
	{
	}

	public FileReader(int len)
	{
		Bytes = new byte[len];
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			Bytes = null;
		}
	}

	public virtual CharCode Read(FileInfo file)
	{
		Length = 0;
		text = null;
		try
		{
			if (file.Length == 0L)
			{
				return FileType.EMPTYFILE;
			}
			if (file.Length > Bytes.Length)
			{
				return FileType.HUGEFILE;
			}
			CharCode charCode;
			using (FileStream fileStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				long length = fileStream.Length;
				if (length == 0L)
				{
					return FileType.EMPTYFILE;
				}
				if (length > Bytes.Length)
				{
					return FileType.HUGEFILE;
				}
				if (length > 65536)
				{
					Length = fileStream.Read(Bytes, 0, 32);
					charCode = GetPreamble(length);
					if (charCode == null || charCode is CharCode.Text)
					{
						Length += fileStream.Read(Bytes, Length, (int)length - Length);
					}
				}
				else
				{
					Length = fileStream.Read(Bytes, 0, (int)length);
					charCode = GetPreamble(length);
				}
			}
			if (charCode is CharCode.Text)
			{
				if ((text = charCode.GetString(Bytes, Length)) == null)
				{
					charCode = null;
				}
			}
			else if (charCode == null)
			{
				charCode = ReadJEnc.GetEncoding(Bytes, Length, out text);
			}
			return (charCode == null) ? FileType.GetBinaryType(Bytes, Length) : charCode;
		}
		catch (IOException)
		{
			return FileType.READERROR;
		}
		catch (UnauthorizedAccessException)
		{
			return FileType.READERROR;
		}
	}

	protected virtual CharCode GetPreamble(long len)
	{
		CharCode preamble = CharCode.GetPreamble(Bytes, Length);
		if (preamble == null && Array.IndexOf(Bytes, (byte)0, 0, Length) >= 0 && ReadJEnc.SeemsUTF16N(Bytes, (int)len) == null)
		{
			return FileType.GetBinaryType(Bytes, Length);
		}
		return preamble;
	}
}
