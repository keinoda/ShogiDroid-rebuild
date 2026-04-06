using System.Collections.Generic;
using System.Linq;

namespace ShogiLib;

public static class HandicapExtension
{
	public static readonly Dictionary<string, Handicap> HandicapHash = new Dictionary<string, Handicap>
	{
		{ "平手", Handicap.HIRATE },
		{ "香落ち", Handicap.KYO },
		{ "右香落ち", Handicap.RIGHT_KYO },
		{ "角落ち", Handicap.KAKU },
		{ "飛車落ち", Handicap.HISYA },
		{ "飛香落ち", Handicap.HIKYO },
		{ "二枚落ち", Handicap.H2 },
		{ "三枚落ち", Handicap.H3 },
		{ "四枚落ち", Handicap.H4 },
		{ "五枚落ち", Handicap.H5 },
		{ "左五枚落ち", Handicap.LEFT5 },
		{ "六枚落ち", Handicap.H6 },
		{ "八枚落ち", Handicap.H8 },
		{ "十枚落ち", Handicap.H10 },
		{ "その他", Handicap.OTHER }
	};

	public static bool IsSenGo(this Handicap handicap)
	{
		return handicap == Handicap.HIRATE || handicap == Handicap.OTHER;
	}

	public static string ToKifuString(this Handicap handicap)
	{
		foreach (var item in HandicapHash)
		{
			if (item.Value == handicap)
			{
				return item.Key;
			}
		}
		return string.Empty;
	}
}
