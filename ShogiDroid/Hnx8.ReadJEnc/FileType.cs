namespace Hnx8.ReadJEnc;

public class FileType : CharCode
{
	public class Bin : CharCode
	{
		internal Bin(string Name, params byte[] Bytes)
			: base(Name, 0, Bytes)
		{
		}

		internal Bin(int Encoding, string Name, params byte[] bytes)
			: base(Name, Encoding, bytes)
		{
		}
	}

	public class ZipBinary : Bin
	{
		internal ZipBinary(string Name, params byte[] Bytes)
			: base(Name, Bytes)
		{
		}
	}

	public class Image : CharCode
	{
		internal Image(string Name, params byte[] Bytes)
			: base(Name, 0, Bytes)
		{
		}
	}

	public static readonly FileType READERROR = new FileType("読込不能");

	public static readonly FileType EMPTYFILE = new FileType("空File");

	public static readonly FileType HUGEFILE = new FileType("巨大File");

	public static readonly Bin BINARY = new Bin("$BINARY", null);

	public static readonly Bin JAVABIN = new Bin(-65001, "$JavaBin", 202, 254, 186, 190);

	public static readonly Bin WINBIN = new Bin("$WinExec", 77, 90);

	public static readonly Bin SHORTCUT = new Bin("$WinLnk", 76, 0, 0, 0, 1, 20, 2, 0);

	public static readonly Bin PDF = new Bin("%PDF", 37, 80, 68, 70, 45);

	public static readonly Bin ZIP = new ZipBinary("$ZIP", 80, 75, 3, 4);

	public static readonly Bin GZIP = new ZipBinary("$GZIP", 31, 139);

	public static readonly Bin SEVENZIP = new ZipBinary("$7ZIP", 55, 122, 188, 175, 39, 28);

	public static readonly Bin RAR = new ZipBinary("$RAR", 82, 97, 114, 33);

	public static readonly Bin CABINET = new ZipBinary("$CAB", 77, 83, 67, 70, 0, 0, 0, 0);

	public static readonly Bin BZIP2 = new ZipBinary("$BZIP2", 66, 90, 104);

	public static readonly Bin ZLZW = new ZipBinary("$Z-LZW", 31, 157);

	public static readonly Image BMP = new Image("%BMP", 66, 77);

	public static readonly Image GIF = new Image("%GIF", 71, 73, 70, 56);

	public static readonly Image JPEG = new Image("%JPEG", 255, 216, 255);

	public static readonly Image PNG = new Image("%PNG", 137, 80, 78, 71, 13, 10, 26, 10);

	public static readonly Image TIFF = new Image("%TIFF", 73, 73, 42, 0);

	public static readonly Image IMGICON = new Image("%ICON", 0, 0, 1, 0);

	public const int GetBinaryType_LEASTREADSIZE = 32;

	public static CharCode GetBinaryType(byte[] bytes, int read)
	{
		CharCode charCode = CharCode.GetPreamble(bytes, read, BMP, GIF, JPEG, PNG, TIFF, IMGICON, JAVABIN, WINBIN, SHORTCUT, PDF, ZIP, GZIP, SEVENZIP, RAR, CABINET, BZIP2, ZLZW);
		if (charCode == IMGICON && (read < 23 || bytes[4] == 0 || bytes[5] != 0))
		{
			charCode = null;
		}
		if (charCode == null)
		{
			return BINARY;
		}
		return charCode;
	}

	private FileType(string Name)
		: base(Name, 0, null)
	{
	}
}
