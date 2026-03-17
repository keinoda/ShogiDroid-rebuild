using ShogiDroid;
using Android.App;

namespace ShogiLib;

public static class MoveEvalExtention
{
	private static string[] moveEvalStr = new string[9]
	{
		string.Empty,
		"-",
		"○",
		"・",
		"！",
		Application.Context.GetString(Resource.String.GoodMove_Text) + "!",
		Application.Context.GetString(Resource.String.DubiousMove_Text) + "?",
		Application.Context.GetString(Resource.String.BadMove_Text) + "?",
		"？"
	};

	private static string blackWonPosition_Text = Application.Context.GetString(Resource.String.BlackWonPosition_Text);

	private static string blackBetterPosition_Text = Application.Context.GetString(Resource.String.BlackBetterPosition_Text);

	private static string blackAdvantage_Text = Application.Context.GetString(Resource.String.BlackAdvantage_Text);

	private static string whiteWonPosition_Text = Application.Context.GetString(Resource.String.WhiteWonPosition_Text);

	private static string whiteBetterPosition_Text = Application.Context.GetString(Resource.String.WhiteBetterPosition_Text);

	private static string whiteAdvantage_Text = Application.Context.GetString(Resource.String.WhiteAdvantage_Text);

	private static string balanced_Text = Application.Context.GetString(Resource.String.Balanced_Text);

	public static string ToString(MoveEval eval)
	{
		return moveEvalStr[(int)eval];
	}

	public static string GetEvalMoveString(MoveDataEx move, MoveDataEx prev)
	{
		return ToString(GetMoveEval(move, prev));
	}

	public static int GetMoveValuel(MoveDataEx move, MoveDataEx prev)
	{
		int num = 0;
		if (prev != null && move.HasScore && prev.HasScore)
		{
			num = move.Score - prev.Score;
			if (move.Turn == PlayerColor.White)
			{
				num = -num;
			}
		}
		return num;
	}

	public static MoveEval GetMoveEval(MoveDataEx move, MoveDataEx prev)
	{
		MoveEval result = MoveEval.None;
		if (move.BestMove == MoveMatche.Best)
		{
			result = MoveEval.Same;
		}
		if (prev != null)
		{
			if (!move.HasScore)
			{
				result = MoveEval.NoSet;
			}
			if (move.HasScore && prev.HasScore)
			{
				int num = move.Score - prev.Score;
				if (move.Turn == PlayerColor.White)
				{
					num = -num;
				}
				if (move.BestMove != MoveMatche.Best)
				{
					result = MoveEval.Par;
				}
				if ((move.Score >= -1500 || prev.Score >= -1500) && (move.Score < 1500 || prev.Score < 1500))
				{
					if (num < -500)
					{
						result = MoveEval.Blunder;
					}
					else if (num < -300)
					{
						result = MoveEval.Bad;
					}
					else if (num < -100)
					{
						if (move.BestMove != MoveMatche.Best)
						{
							result = MoveEval.Weak;
						}
					}
					else if (num > 300)
					{
						if (move.BestMove != MoveMatche.Best)
						{
							result = MoveEval.Best;
						}
					}
					else if (num > 100 && move.BestMove != MoveMatche.Best)
					{
						result = MoveEval.Good;
					}
				}
			}
		}
		return result;
	}

	public static string GetStateString(MoveDataEx move)
	{
		string result = string.Empty;
		int num = move.Score;
		if (num == 0)
		{
			num = move.Eval;
		}
		if (move.HasEval || move.HasScore)
		{
			result = ((num > 1500) ? blackWonPosition_Text : ((num > 800) ? blackBetterPosition_Text : ((num > 300) ? blackAdvantage_Text : ((num < -1500) ? whiteWonPosition_Text : ((num < -800) ? whiteBetterPosition_Text : ((num >= -300) ? balanced_Text : whiteAdvantage_Text))))));
		}
		return result;
	}
}
