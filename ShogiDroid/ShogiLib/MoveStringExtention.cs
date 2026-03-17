namespace ShogiLib;

public static class MoveStringExtention
{
	public static string ToString(this MoveData move, MoveStyle type, SPosition position, RankStyle rankStyle = RankStyle.Number)
	{
		switch (type)
		{
		case MoveStyle.Traditional:
			if (position == null)
			{
				return Kifu.GetMoveString(move);
			}
			return Ki2.GetMoveString(position, move);
		case MoveStyle.English:
			return EnglishNotation.MoveString(move, position, rankStyle);
		default:
			return Kifu.GetMoveString(move);
		}
	}

	public static string ToString(this MoveDataEx move, MoveStyle type, RankStyle rankStyle)
	{
		return type switch
		{
			MoveStyle.Traditional => Ki2.GetMoveString(move), 
			MoveStyle.English => EnglishNotation.MoveString(move, rankStyle), 
			_ => Kifu.GetMoveString(move), 
		};
	}

	public static string ToString(this MoveDataEx move, MoveStyle type)
	{
		return move.ToString(type, RankStyle.Number);
	}

	public static string InitialPosition(MoveStyle type)
	{
		if (type != MoveStyle.Traditional && type == MoveStyle.English)
		{
			return "Start";
		}
		return "開始局面";
	}

	public static string ToEvalString(int eval, MoveStyle style)
	{
		string empty = string.Empty;
		if (eval >= 31900)
		{
			int num = 32000 - eval;
			empty = ((style != MoveStyle.English) ? "詰" : "mate ");
			if (num != 0)
			{
				empty += num;
			}
		}
		else if (eval <= -31900)
		{
			int num2 = 32000 + eval;
			empty = ((style != MoveStyle.English) ? "-詰" : "-mate ");
			if (num2 != 0)
			{
				empty += num2;
			}
		}
		else
		{
			empty = eval.ToString();
		}
		return empty;
	}
}
