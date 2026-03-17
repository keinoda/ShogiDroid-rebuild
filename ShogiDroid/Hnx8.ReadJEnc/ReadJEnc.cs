using System;

namespace Hnx8.ReadJEnc;

public class ReadJEnc
{
	private class JIS
	{
		private byte[] bytes;

		private int len;

		private bool JISH;

		private bool ISOKR;

		private int c;

		internal static bool hasSOSI(byte[] bytes, int len)
		{
			if (Array.IndexOf(bytes, (byte)14, 0, len) >= 0)
			{
				return Array.IndexOf(bytes, (byte)15, 0, len) >= 0;
			}
			return false;
		}

		internal JIS(byte[] bytes, int len, int pos)
		{
			this.bytes = bytes;
			this.len = len;
			ISOKR = pos >= 0 && pos < len - 4 && bytes[pos + 1] == 36 && bytes[pos + 2] == 41 && bytes[pos + 3] == 67;
		}

		internal int GetEncoding(int pos)
		{
			if (pos + 2 < len)
			{
				c++;
				switch (bytes[pos + 1])
				{
				case 36:
					switch (bytes[pos + 2])
					{
					case 64:
					case 66:
						return 2;
					case 40:
						if (pos + 3 < len && bytes[pos + 3] == 68)
						{
							JISH = true;
							return 3;
						}
						break;
					}
					break;
				case 40:
				{
					byte b = bytes[pos + 2];
					if (b == 66 || (uint)(b - 72) <= 2u)
					{
						return 2;
					}
					break;
				}
				}
			}
			c -= 4;
			return 0;
		}

		internal CharCode GetEncoding(out string text)
		{
			byte[] array = bytes;
			int num = len;
			if (ISOKR && hasSOSI(array, num))
			{
				text = CharCode.ISOKR.GetString(array, num);
				if (text == null)
				{
					return null;
				}
				return CharCode.ISOKR;
			}
			if (c <= 0)
			{
				text = null;
				return null;
			}
			if (JISH)
			{
				text = CharCode.JISH.GetString(array, num);
				if (text != null)
				{
					return CharCode.JISH;
				}
			}
			else
			{
				text = CharCode.JIS.GetString(array, num);
				if (text != null)
				{
					if (!hasSOSI(array, num))
					{
						return CharCode.JIS;
					}
					return CharCode.JIS50222;
				}
			}
			text = CharCode.JISNG.GetString(array, num);
			return CharCode.JISNG;
		}
	}

	private class SJIS : ReadJEnc
	{
		internal SJIS()
			: base(CharCode.SJIS, CharCode.EUC)
		{
		}

		protected override int GetEncoding(byte[] bytes, int pos, int len)
		{
			int num = 0;
			byte b = bytes[pos];
			while (pos < len)
			{
				int num2 = 0;
				while (b > 127)
				{
					if (b >= 161 && b <= 223)
					{
						if (num2 == 1)
						{
							num += 3;
						}
						else
						{
							num++;
							num2 = 1;
						}
					}
					else
					{
						byte b2;
						if ((((b < 224) ? 97 : (-536832000)) & (1 << b % 32)) != 0 || ++pos >= len || (b2 = bytes[pos]) < 64 || b2 > 252)
						{
							return -2147483648;
						}
						if (num2 == 2)
						{
							num += 4;
						}
						else
						{
							num += ((b <= 152) ? 2 : 0);
							num2 = 2;
						}
					}
					if (++pos >= len)
					{
						break;
					}
					b = bytes[pos];
				}
				while (b <= 127 && ++pos < len)
				{
					b = bytes[pos];
				}
			}
			return num;
		}
	}

	private class BIG5TW : ReadJEnc
	{
		internal BIG5TW()
			: base(CharCode.BIG5TW, CharCode.EUCTW)
		{
		}

		protected override int GetEncoding(byte[] bytes, int pos, int len)
		{
			int num = 0;
			byte b = bytes[pos];
			while (pos < len)
			{
				int num2 = 0;
				while (b > 127)
				{
					byte b2;
					if (b < 129 || b > 249 || b == 199 || b == 200 || ++pos >= len || (b2 = bytes[pos]) < 64 || (b2 < 161 && b2 > 126) || b2 > 254)
					{
						return -2147483648;
					}
					if (num2 == 2)
					{
						num += 4;
					}
					else
					{
						num += ((b < 161 || b > 200) ? 1 : 2);
						num2 = 2;
					}
					if (++pos >= len)
					{
						break;
					}
					b = bytes[pos];
				}
				while (b <= 127 && ++pos < len)
				{
					b = bytes[pos];
				}
			}
			return num;
		}
	}

	private class GB18030 : ReadJEnc
	{
		internal GB18030()
			: base(CharCode.GB18030, CharCode.GB18030)
		{
		}

		protected override int GetEncoding(byte[] bytes, int pos, int len)
		{
			int num = 0;
			byte b = bytes[pos];
			while (pos < len)
			{
				int num2 = 0;
				while (b > 127)
				{
					if (b < 129 || b > 254 || ++pos >= len)
					{
						return -2147483648;
					}
					byte b2;
					if ((b2 = bytes[pos]) >= 64 && b2 <= 254)
					{
						if (num2 == 2)
						{
							num += 4;
						}
						else
						{
							num += 2;
							num2 = 2;
						}
					}
					else
					{
						if (b2 < 48 || b2 > 57 || ++pos >= len || (b2 = bytes[pos]) < 129 || b2 > 254 || ++pos >= len || (b2 = bytes[pos]) < 48 || b2 > 57)
						{
							return -2147483648;
						}
						if (num2 == 2)
						{
							num += 16;
						}
						else
						{
							num += 8;
							num2 = 2;
						}
					}
					if (++pos >= len)
					{
						break;
					}
					b = bytes[pos];
				}
				while (b <= 127 && ++pos < len)
				{
					b = bytes[pos];
				}
			}
			return num;
		}
	}

	private class UHCKR : ReadJEnc
	{
		internal UHCKR()
			: base(CharCode.UHCKR, CharCode.UHCKR)
		{
		}

		protected override int GetEncoding(byte[] bytes, int pos, int len)
		{
			int num = 0;
			byte b = bytes[pos];
			while (pos < len)
			{
				int num2 = 0;
				while (b > 127)
				{
					byte b2;
					if (b < 129 || b > 254 || ++pos >= len || (b2 = bytes[pos]) < 65 || (b2 < 97 && b2 > 90) || (b2 < 129 && b2 > 122) || b2 > 254)
					{
						return -2147483648;
					}
					if (num2 == 2)
					{
						num += 4;
					}
					else
					{
						num += 2;
						num2 = 2;
					}
					if (++pos >= len)
					{
						break;
					}
					b = bytes[pos];
				}
				while (b <= 127 && ++pos < len)
				{
					b = bytes[pos];
				}
			}
			return num;
		}
	}

	private class SBCS : ReadJEnc
	{
		private int BOUND;

		private new readonly uint[] NODEF;

		internal SBCS(CharCode CharCode, int BOUND, params uint[] NODEF)
			: base(CharCode, null)
		{
			this.BOUND = BOUND;
			this.NODEF = NODEF;
		}

		protected override int GetEncoding(byte[] bytes, int pos, int len)
		{
			int num = 0;
			byte b = bytes[pos];
			uint num2 = ((NODEF.Length != 0) ? NODEF[0] : 0u);
			uint num3 = ((NODEF.Length > 1) ? NODEF[1] : 0u);
			uint num4 = ((NODEF.Length > 2) ? NODEF[2] : 0u);
			uint num5 = ((NODEF.Length > 3) ? NODEF[3] : 0u);
			while (pos < len)
			{
				bool flag = false;
				while (b > 127)
				{
					if ((((b >= 192) ? ((b < 224) ? num4 : num5) : ((b < 160) ? num2 : num3)) & (uint)(1 << b % 32)) != 0)
					{
						return -2147483648;
					}
					if (b >= BOUND)
					{
						if (flag)
						{
							num += 3;
						}
						else
						{
							num++;
							flag = true;
						}
					}
					else if (flag)
					{
						num++;
						flag = false;
					}
					else
					{
						num += 2;
					}
					if (++pos >= len)
					{
						break;
					}
					b = bytes[pos];
				}
				while (b <= 127 && ++pos < len)
				{
					b = bytes[pos];
				}
			}
			return num;
		}
	}

	public static readonly ReadJEnc JP = new SJIS();

	public static readonly ReadJEnc ANSI = new ReadJEnc(CharCode.ANSI, null);

	public static readonly ReadJEnc TW = new BIG5TW();

	public static readonly ReadJEnc CN = new GB18030();

	public static readonly ReadJEnc KR = new UHCKR();

	public static readonly ReadJEnc CP1250 = new ReadJEnc(CharCode.CP1250, 16843018u);

	public static readonly ReadJEnc CP1251 = new SBCS(CharCode.CP1251, 192, 16777216u);

	public static readonly ReadJEnc CP1253 = new SBCS(CharCode.CP1253, 193, 4110546178u, 1024u, 262144u, 2147483648u);

	public static readonly ReadJEnc CP1254 = new ReadJEnc(CharCode.CP1254, 1610735618u);

	public static readonly ReadJEnc CP1255 = new SBCS(CharCode.CP1255, 192, 4093768706u, 0u, 4261413888u, 2550136832u);

	public static readonly ReadJEnc CP1256 = new SBCS(CharCode.CP1256, 192);

	public static readonly ReadJEnc CP1257 = new ReadJEnc(CharCode.CP1257, 2499876106u);

	public static readonly ReadJEnc CP1258 = new ReadJEnc(CharCode.CP1258, 1677845506u);

	public static readonly ReadJEnc TIS620 = new SBCS(CharCode.TIS620, 161, 4278321118u, 0u, 2013265920u, 4026531840u);

	private const byte DEL = 127;

	private const byte BINARY = 3;

	public readonly CharCode CharCode;

	protected readonly CharCode EUC;

	protected readonly CharCode CP125X = CharCode.ANSI;

	protected readonly uint NODEF = 536977410u;

	protected ReadJEnc(CharCode CharCode, CharCode EUC)
	{
		this.CharCode = CharCode;
		this.EUC = EUC;
	}

	protected ReadJEnc(CharCode CP125X, uint NODEF)
	{
		CharCode = CP125X;
		this.CP125X = CP125X;
		this.NODEF = NODEF;
	}

	public override string ToString()
	{
		return CharCode.Name;
	}

	public CharCode GetEncoding(byte[] bytes, int len, out string text)
	{
		if (len == 0)
		{
			text = null;
			return null;
		}
		byte b = bytes[0];
		JIS jIS = null;
		int num = 0;
		while (b < 127)
		{
			if (b <= 3)
			{
				CharCode charCode = ((num < 2) ? SeemsUTF16N(bytes, len) : null);
				if (charCode != null && (text = charCode.GetString(bytes, len)) != null)
				{
					int i;
					for (i = -3; i <= 3 && text.IndexOf((char)i, 0, text.Length) == -1; i++)
					{
					}
					if (i > 3 && text.IndexOf('\u007f', 0, text.Length) == -1)
					{
						return charCode;
					}
				}
				text = null;
				return null;
			}
			if (b == 27)
			{
				if (jIS == null)
				{
					jIS = new JIS(bytes, len, num);
				}
				num += jIS.GetEncoding(num);
			}
			if (++num >= len)
			{
				if (jIS != null)
				{
					CharCode encoding = jIS.GetEncoding(out text);
					if (encoding != null)
					{
						return encoding;
					}
				}
				else if (JIS.hasSOSI(bytes, len) && jIS == null && (text = CharCode.JIS50222.GetString(bytes, len)) != null)
				{
					return CharCode.JIS50222;
				}
				if ((text = CharCode.ASCII.GetString(bytes, len)) == null)
				{
					return null;
				}
				return CharCode.ASCII;
			}
			b = bytes[num];
		}
		int num2 = 0;
		int num3 = 0;
		int num4 = ((EUC == null) ? (-2147483648) : 0);
		int num5 = 0;
		bool flag = false;
		uint nODEF = NODEF;
		int j = num;
		while (j < len)
		{
			if (b == 127)
			{
				num2 = -2147483648;
				num3 = -2147483648;
				num4 = -2147483648;
				num5 = -2147483648;
				if (jIS == null || j++ >= len || (b = bytes[j]) < 33 || b >= 127)
				{
					text = null;
					return null;
				}
			}
			int num6 = j;
			if (num2 == -2147483648)
			{
				goto IL_0190;
			}
			while (b > 127)
			{
				if (b > 159 || (nODEF & (uint)(1 << b % 32)) == 0)
				{
					if (++j >= len)
					{
						break;
					}
					b = bytes[j];
					continue;
				}
				goto IL_01bd;
			}
			byte b2;
			num2 = ((j == num6 + 1) ? (num2 + 2) : ((j != num6 + 2 || (b2 = bytes[j - 1]) < 192) ? (num2 + 1) : ((b2 == (b2 = bytes[j - 2])) ? (num2 + 5) : ((b2 < 192) ? (num2 + 1) : ((b <= 64 && (num6 <= 0 || bytes[num6 - 1] <= 64)) ? (num2 + 3) : (num2 + 5))))));
			goto IL_024e;
			IL_01bd:
			num2 = -2147483648;
			goto IL_0190;
			IL_0190:
			while (b > 127 && ++j < len)
			{
				b = bytes[j];
			}
			goto IL_024e;
			IL_024e:
			if (num3 >= 0)
			{
				bool flag2 = false;
				int num7;
				for (num7 = num6; num7 < j; num7++)
				{
					b = bytes[num7];
					if (b < 194 || ++num7 >= j || bytes[num7] > 191)
					{
						num3 = -2147483648;
						break;
					}
					if (b < 224)
					{
						if (!flag2)
						{
							num3 += 6;
						}
						else
						{
							num3 += 2;
							flag2 = false;
						}
					}
					else
					{
						if (++num7 >= j || bytes[num7] > 191)
						{
							num3 = -2147483648;
							break;
						}
						if (b < 240)
						{
							if (flag2)
							{
								num3 += 8;
							}
							else
							{
								num3 += 4;
								flag2 = true;
							}
						}
						else
						{
							if (++num7 >= j || bytes[num7] > 191)
							{
								num3 = -2147483648;
								break;
							}
							if (b >= 245)
							{
								num3 = -2147483648;
								break;
							}
							if (flag2)
							{
								num3 += 12;
							}
							else
							{
								num3 += 6;
								flag2 = true;
							}
						}
					}
				}
			}
			if (num4 >= 0)
			{
				int num8 = 0;
				int num9;
				for (num9 = num6; num9 < j; num9++)
				{
					b = bytes[num9];
					if (b == 255 || ++num9 >= j)
					{
						num4 = -2147483648;
						break;
					}
					b2 = bytes[num9];
					if (b >= 161)
					{
						if (b2 < 161 || b2 == 255)
						{
							num4 = -2147483648;
							break;
						}
						if (num8 == 2)
						{
							num4 += 5;
						}
						else
						{
							num4 += 2;
							num8 = 2;
						}
					}
					else if (b == 142)
					{
						if (b2 < 161 || b2 > 223)
						{
							num4 = -2147483648;
							break;
						}
						if (num8 == 1)
						{
							num4 += 6;
						}
						else if (EUC == CharCode.EUCTW)
						{
							if (num8 == 2)
							{
								num4 += 6;
							}
							else
							{
								num4 += 2;
								num8 = 2;
							}
						}
						else
						{
							num4 += 2;
							num8 = 1;
						}
					}
					else
					{
						if (b != 143 || b2 < 161 || b2 >= 255 || ++num9 >= j || (b2 = bytes[num9]) < 161 || b2 >= 255)
						{
							num4 = -2147483648;
							break;
						}
						if (num8 == 2)
						{
							num4 += 8;
						}
						else
						{
							num4 += 3;
							num8 = 2;
						}
						flag = true;
					}
				}
			}
			for (; j < len; j++)
			{
				if ((b = bytes[j]) >= 127)
				{
					break;
				}
				if (b <= 3)
				{
					text = null;
					return null;
				}
				if (b == 27)
				{
					if (jIS == null)
					{
						jIS = new JIS(bytes, len, j);
					}
					j += jIS.GetEncoding(j);
				}
			}
		}
		if (num5 != -2147483648)
		{
			num5 = GetEncoding(bytes, num, len);
		}
		if (jIS != null)
		{
			CharCode encoding2 = jIS.GetEncoding(out text);
			if (encoding2 != null)
			{
				return encoding2;
			}
		}
		if (num4 > 0 && num4 > num5 && num4 > num3)
		{
			if (num2 > num4 && (text = CP125X.GetString(bytes, len)) != null)
			{
				return CP125X;
			}
			if (flag && (text = CharCode.EUCH.GetString(bytes, len)) != null)
			{
				return CharCode.EUCH;
			}
			if ((text = EUC.GetString(bytes, len)) != null)
			{
				return EUC;
			}
		}
		if (num3 > 0 && num3 >= num5 && (text = CharCode.UTF8N.GetString(bytes, len)) != null)
		{
			return CharCode.UTF8N;
		}
		if (num5 >= 0)
		{
			if (num2 > num5 && (text = CP125X.GetString(bytes, len)) != null)
			{
				return CP125X;
			}
			if ((text = CharCode.GetString(bytes, len)) != null)
			{
				return CharCode;
			}
		}
		if (num2 > 0 && (text = CP125X.GetString(bytes, len)) != null)
		{
			return CP125X;
		}
		text = null;
		return null;
	}

	protected virtual int GetEncoding(byte[] bytes, int pos, int len)
	{
		return -2147483648;
	}

	public static CharCode SeemsUTF16N(byte[] bytes, int len)
	{
		if (len >= 2 && len % 2 == 0)
		{
			if (bytes[0] == 0)
			{
				if (bytes[1] > 3 && bytes[1] < 127 && (len == 2 || bytes[2] == 0))
				{
					return CharCode.UTF16BE;
				}
			}
			else if (bytes[1] == 0 && bytes[0] > 3 && bytes[0] < 127 && (len == 2 || bytes[3] == 0))
			{
				return CharCode.UTF16LE;
			}
		}
		return null;
	}
}
