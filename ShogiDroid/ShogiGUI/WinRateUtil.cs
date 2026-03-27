namespace ShogiGUI;

/// <summary>
/// 評価値(cp)と勝率(%)の相互変換ユーティリティ。
/// シグモイド関数: winRate = 1 / (1 + exp(-eval / coefficient))
/// 係数600はPonanza由来で将棋GUIの事実上の標準。
/// </summary>
public static class WinRateUtil
{
	public const int MateEvalThreshold = 31900;

	public const int HisshiEval = 35281;

	/// <summary>
	/// シグモイド変換の係数。600 = Ponanza定数（将棋GUIの標準）。
	/// </summary>
	public const double DefaultCoefficient = 750.0;

	public static bool IsMateScore(int cp)
	{
		return System.Math.Abs(cp) >= MateEvalThreshold;
	}

	public static bool IsHisshiScore(int cp)
	{
		return System.Math.Abs(cp) >= HisshiEval;
	}

	public static bool IsForcedWinScore(int cp)
	{
		return IsMateScore(cp) || IsHisshiScore(cp);
	}

	/// <summary>
	/// 評価値(cp)を勝率(0.0〜1.0)に変換する。
	/// </summary>
	public static double CpToWinRate(int cp, double coefficient = DefaultCoefficient)
	{
		if (IsForcedWinScore(cp))
		{
			return cp > 0 ? 1.0 : 0.0;
		}
		return 1.0 / (1.0 + System.Math.Exp(-(double)cp / coefficient));
	}

	/// <summary>
	/// 評価値(cp)を勝率パーセント(0〜100)に変換する。
	/// </summary>
	public static double CpToWinPercent(int cp, double coefficient = DefaultCoefficient)
	{
		return CpToWinRate(cp, coefficient) * 100.0;
	}

	/// <summary>
	/// 勝率(0.0〜1.0)を評価値(cp)に変換する。
	/// </summary>
	public static int WinRateToCp(double winRate, double coefficient = DefaultCoefficient)
	{
		if (winRate <= 0.0) return -9999;
		if (winRate >= 1.0) return 9999;
		return (int)(coefficient * System.Math.Log(winRate / (1.0 - winRate)));
	}

	/// <summary>
	/// 評価値(cp)を表示用文字列に変換。詰みの場合は "詰み N手"。
	/// </summary>
	public static string FormatEval(int cp, bool isMate, int matePly)
	{
		if (isMate)
		{
			if (matePly > 0)
				return $"詰み {matePly}手";
			else if (matePly < 0)
				return $"被詰み {-matePly}手";
			else
				return cp < 0 ? "被詰み" : "詰み";
		}
		return cp >= 0 ? $"+{cp}" : cp.ToString();
	}

	/// <summary>
	/// 評価値を勝率表示文字列に変換 (例: "56.3%")。
	/// </summary>
	public static string FormatWinRate(int cp, bool isMate, int matePly, double coefficient = DefaultCoefficient)
	{
		if (isMate)
		{
			if (matePly > 0)
				return $"詰み {matePly}手";
			if (matePly < 0)
				return $"被詰み {-matePly}手";
			return cp < 0 ? "被詰み" : "詰み";
		}
		if (IsHisshiScore(cp))
		{
			return cp > 0 ? "先手必至" : "後手必至";
		}
		int pct = (int)CpToWinPercent(cp, coefficient);
		if (pct > 100) pct = 100;
		if (pct < 0) pct = 0;
		return $"{pct}%";
	}

	/// <summary>
	/// 指し手の評価損失から手の分類を返す。
	/// 評価値の差分(前の手の評価 - この手の評価)をwinRate損失に変換して判定。
	/// </summary>
	public static MoveGrade ClassifyMove(int evalBefore, int evalAfter, double coefficient = DefaultCoefficient)
	{
		double winRateBefore = CpToWinRate(evalBefore, coefficient);
		double winRateAfter = CpToWinRate(evalAfter, coefficient);
		double loss = winRateBefore - winRateAfter;

		if (loss < 0.02) return MoveGrade.Best;
		if (loss < 0.05) return MoveGrade.Good;
		if (loss < 0.10) return MoveGrade.Inaccuracy;
		if (loss < 0.20) return MoveGrade.Mistake;
		return MoveGrade.Blunder;
	}

	/// <summary>
	/// 手の分類を日本語表示に変換。
	/// </summary>
	public static string GradeToString(MoveGrade grade)
	{
		switch (grade)
		{
			case MoveGrade.Best: return "最善";
			case MoveGrade.Good: return "好手";
			case MoveGrade.Inaccuracy: return "疑問手";
			case MoveGrade.Mistake: return "緩手";
			case MoveGrade.Blunder: return "悪手";
			default: return "";
		}
	}

	/// <summary>
	/// 手の分類を記号に変換。
	/// </summary>
	public static string GradeToSymbol(MoveGrade grade)
	{
		switch (grade)
		{
			case MoveGrade.Best: return "○";
			case MoveGrade.Good: return "";
			case MoveGrade.Inaccuracy: return "?!";
			case MoveGrade.Mistake: return "?";
			case MoveGrade.Blunder: return "??";
			default: return "";
		}
	}
}

/// <summary>
/// 指し手の分類（品質）。
/// </summary>
public enum MoveGrade
{
	None = 0,
	Best,        // 最善手
	Good,        // 好手
	Inaccuracy,  // 疑問手
	Mistake,     // 緩手
	Blunder      // 悪手
}
