using System.Text;

namespace Hnx8.ReadJEnc;

public abstract class CharCode
{
	public class Text : CharCode
	{
		internal Text(string Name, Encoding Encoding)
			: base(Name, Encoding, Encoding.GetPreamble())
		{
		}

		internal Text(string Name, int CodePage)
			: base(Name, CodePage, null)
		{
		}
	}

	private class EucHText : Text
	{
		internal EucHText(string Name)
			: base(Name, 20932)
		{
		}

		public override string GetString(byte[] bytes, int len)
		{
			byte[] array = new byte[len];
			int num = 0;
			int num2 = -2147483648;
			for (int i = 0; i < len; i++)
			{
				byte b;
				if ((b = bytes[i]) == 143)
				{
					num2 = i + 2;
					continue;
				}
				array[num] = ((i == num2) ? ((byte)(b & 0x7F)) : b);
				num++;
			}
			try
			{
				return GetEncoding().GetString(array, 0, num);
			}
			catch (DecoderFallbackException)
			{
				return null;
			}
		}
	}

	private class JisHText : Text
	{
		internal JisHText(string Name)
			: base(Name, 0)
		{
		}

		public override string GetString(byte[] bytes, int len)
		{
			try
			{
				StringBuilder stringBuilder = new StringBuilder(len);
				int i = 0;
				while (i < len)
				{
					int num = i;
					for (; i < len && (bytes[i] != 27 || i + 3 >= len || bytes[i + 1] != 36 || bytes[i + 2] != 40 || bytes[i + 3] != 68); i++)
					{
					}
					if (num < i)
					{
						stringBuilder.Append(JIS.GetEncoding().GetString(bytes, num, i - num));
					}
					if (i >= len)
					{
						continue;
					}
					i += 4;
					num = i;
					for (; i < len && bytes[i] != 27; i++)
					{
					}
					if (num >= i)
					{
						continue;
					}
					byte[] array = new byte[i - num];
					for (int j = 0; j < array.Length; j++)
					{
						array[j] = bytes[num + j];
						if (j % 2 == 0)
						{
							array[j] |= 128;
						}
					}
					stringBuilder.Append(EUCH.GetEncoding().GetString(array, 0, array.Length));
				}
				return stringBuilder.ToString();
			}
			catch (DecoderFallbackException)
			{
				return null;
			}
		}
	}

	public static readonly Text UTF8 = new Text("UTF-8", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true));

	public static readonly Text UTF32 = new Text("UTF-32", new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true));

	public static readonly Text UTF32B = new Text("UTF-32B", new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true));

	public static readonly Text UTF16 = new Text("UTF-16", new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true));

	public static readonly Text UTF16B = new Text("UTF-16B", new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true));

	public static readonly Text UTF16LE = new Text("UTF-16LE", new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true));

	public static readonly Text UTF16BE = new Text("UTF-16BE", new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true));

	public static readonly Text UTF8N = new Text("UTF-8N", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));

	public static readonly Text ASCII = new Text("ASCII", 0)
	{
		Encoding = UTF8N.GetEncoding()
	};

	public static readonly Text ANSI = new Text("ANSI1252", 1252);

	public static readonly Text JIS = new Text("JIS", 50221);

	public static readonly Text JIS50222 = new Text("JIS50222", 50222);

	public static readonly Text JISH = new JisHText("JIS補漢");

	public static readonly Text JISNG = new Text("JIS破損", -50221);

	public static readonly Text ISOKR = new Text("ISO-KR", 50225);

	public static readonly Text SJIS = new Text("ShiftJIS", 932);

	public static readonly Text EUCH = new EucHText("EUC補漢");

	public static readonly Text EUC = new Text("EUCJP", 51932);

	public static readonly Text BIG5TW = new Text("Big5", 950);

	public static readonly Text EUCTW = new Text("EUC-TW", 20000);

	public static readonly Text GB18030 = new Text("GB18030", 54936);

	public static readonly Text UHCKR = new Text("UHC", 949);

	public static readonly Text CP1250 = new Text("CP1250", 1250);

	public static readonly Text CP1251 = new Text("CP1251", 1251);

	public static readonly Text CP1253 = new Text("CP1253", 1253);

	public static readonly Text CP1254 = new Text("CP1254", 1254);

	public static readonly Text CP1255 = new Text("CP1255", 1255);

	public static readonly Text CP1256 = new Text("CP1256", 1256);

	public static readonly Text CP1257 = new Text("CP1257", 1257);

	public static readonly Text CP1258 = new Text("CP1258", 1258);

	public static readonly Text TIS620 = new Text("TIS-620", 874);

	public readonly string Name;

	protected readonly byte[] Bytes;

	private Encoding Encoding;

	public readonly int CodePage;

	public static CharCode GetPreamble(byte[] bytes, int read)
	{
		return GetPreamble(bytes, read, UTF8, UTF32, UTF32B, UTF16, UTF16B);
	}

	protected CharCode(string Name, int CodePage, byte[] Bytes)
	{
		this.Name = Name;
		this.CodePage = CodePage;
		this.Bytes = Bytes;
	}

	protected CharCode(string Name, Encoding Encoding, byte[] Bytes)
	{
		this.Name = Name;
		this.Encoding = Encoding;
		this.Bytes = Bytes;
	}

	public Encoding GetEncoding()
	{
		if (Encoding == null)
		{
			Encoding = ((CodePage > 0) ? Encoding.GetEncoding(CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) : ((CodePage < 0) ? Encoding.GetEncoding(-CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback) : null));
		}
		return Encoding;
	}

	public virtual string GetString(byte[] bytes, int len)
	{
		Encoding encoding = GetEncoding();
		if (encoding == null)
		{
			return null;
		}
		try
		{
			int num = ((Bytes != null) ? Bytes.Length : 0);
			return encoding.GetString(bytes, num, len - num);
		}
		catch (DecoderFallbackException)
		{
			return null;
		}
	}

	public override string ToString()
	{
		return Name;
	}

	protected static CharCode GetPreamble(byte[] bytes, int read, params CharCode[] arr)
	{
		foreach (CharCode charCode in arr)
		{
			byte[] bytes2 = charCode.Bytes;
			int num = ((bytes2 != null) ? bytes2.Length : 2147483647);
			if (read < num)
			{
				continue;
			}
			do
			{
				if (num == 0)
				{
					return charCode;
				}
				num--;
			}
			while (bytes[num] == bytes2[num]);
		}
		return null;
	}
}
