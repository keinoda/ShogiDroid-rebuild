namespace ShogiLib;

public static class Square
{
	public const int SQ91 = 0;

	public const int SQ81 = 1;

	public const int SQ71 = 2;

	public const int SQ61 = 3;

	public const int SQ51 = 4;

	public const int SQ41 = 5;

	public const int SQ31 = 6;

	public const int SQ21 = 7;

	public const int SQ11 = 8;

	public const int SQ92 = 9;

	public const int SQ82 = 10;

	public const int SQ72 = 11;

	public const int SQ62 = 12;

	public const int SQ52 = 13;

	public const int SQ42 = 14;

	public const int SQ32 = 15;

	public const int SQ22 = 16;

	public const int SQ12 = 17;

	public const int SQ93 = 18;

	public const int SQ83 = 19;

	public const int SQ73 = 20;

	public const int SQ63 = 21;

	public const int SQ53 = 22;

	public const int SQ43 = 23;

	public const int SQ33 = 24;

	public const int SQ23 = 25;

	public const int SQ13 = 26;

	public const int SQ94 = 27;

	public const int SQ84 = 28;

	public const int SQ74 = 29;

	public const int SQ64 = 30;

	public const int SQ54 = 31;

	public const int SQ44 = 32;

	public const int SQ34 = 33;

	public const int SQ24 = 34;

	public const int SQ14 = 35;

	public const int SQ95 = 36;

	public const int SQ85 = 37;

	public const int SQ75 = 38;

	public const int SQ65 = 39;

	public const int SQ55 = 40;

	public const int SQ45 = 41;

	public const int SQ35 = 42;

	public const int SQ25 = 43;

	public const int SQ15 = 44;

	public const int SQ96 = 45;

	public const int SQ86 = 46;

	public const int SQ76 = 47;

	public const int SQ66 = 48;

	public const int SQ56 = 49;

	public const int SQ46 = 50;

	public const int SQ36 = 51;

	public const int SQ26 = 52;

	public const int SQ16 = 53;

	public const int SQ97 = 54;

	public const int SQ87 = 55;

	public const int SQ77 = 56;

	public const int SQ67 = 57;

	public const int SQ57 = 58;

	public const int SQ47 = 59;

	public const int SQ37 = 60;

	public const int SQ27 = 61;

	public const int SQ17 = 62;

	public const int SQ98 = 63;

	public const int SQ88 = 64;

	public const int SQ78 = 65;

	public const int SQ68 = 66;

	public const int SQ58 = 67;

	public const int SQ48 = 68;

	public const int SQ38 = 69;

	public const int SQ28 = 70;

	public const int SQ18 = 71;

	public const int SQ99 = 72;

	public const int SQ89 = 73;

	public const int SQ79 = 74;

	public const int SQ69 = 75;

	public const int SQ59 = 76;

	public const int SQ49 = 77;

	public const int SQ39 = 78;

	public const int SQ29 = 79;

	public const int SQ19 = 80;

	public const int NFILE = 9;

	public const int NRANK = 9;

	public const int NSQUARE = 81;

	public static int FileOf(this int sq)
	{
		return sq % 9;
	}

	public static int RankOf(this int sq)
	{
		return sq / 9;
	}

	public static int SujiOf(this int sq)
	{
		return 9 - sq % 9;
	}

	public static int DanOf(this int sq)
	{
		return sq / 9 + 1;
	}

	public static int Make(File file, Rank rank)
	{
		return (int)((int)rank * 9 + file);
	}

	public static int Make(int file, int rank)
	{
		return rank * 9 + file;
	}

	public static bool InBoard(int sq)
	{
		if (sq >= 0 && sq < 81)
		{
			return true;
		}
		return false;
	}

	public static int ToSuji(this int file)
	{
		return 9 - file;
	}

	public static int ToFile(this int suji)
	{
		return 9 - suji;
	}

	public static int ToDan(this int rank)
	{
		return rank + 1;
	}

	public static int ToRank(this int dan)
	{
		return dan - 1;
	}
}
