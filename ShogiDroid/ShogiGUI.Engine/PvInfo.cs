using ShogiDroid;
using System.Collections.Generic;
using Android.App;
using ShogiGUI;
using ShogiLib;

namespace ShogiGUI.Engine;

public class PvInfo
{
	public const int MateNone = 0;

	public const int MateBlack = 1;

	public const int MateWhite = -1;

	private List<MoveDataEx> pvmoves_;

	private int? rank;

	private int? time;

	private int? depth;

	private int? selDepth;

	private long? nodes;

	private int? score;

	private int? mate;

	private string message;

	private int? nps;

	private int bounds;

	private static string rankText = Application.Context.GetString(Resource.String.Rank_Text);

	private static string timeText = Application.Context.GetString(Resource.String.Time_Text);

	private static string depthText = Application.Context.GetString(Resource.String.Depth_Text);

	private static string nodesText = Application.Context.GetString(Resource.String.Nodes_Text);

	private static string valueText = Application.Context.GetString(Resource.String.Value_Text);

	private static string movesText = Application.Context.GetString(Resource.String.Moves_Text);

	private static string mateText = Application.Context.GetString(Resource.String.Mate_Text);

	private static string stringText = Application.Context.GetString(Resource.String.String_Text);

	public AnalyzeCommentKind Kind { get; set; }

	public bool HasRank => rank.HasValue;

	public int Rank
	{
		get
		{
			return rank.GetValueOrDefault();
		}
		set
		{
			rank = value;
		}
	}

	public bool HasTimeMs => time.HasValue;

	public int TimeMs
	{
		get
		{
			return time.GetValueOrDefault();
		}
		set
		{
			time = value;
		}
	}

	public bool HasScore => score.HasValue;

	public int Score
	{
		get
		{
			return score.GetValueOrDefault();
		}
		set
		{
			score = value;
		}
	}

	public bool HasMate => mate.HasValue;

	public int Mate
	{
		get
		{
			return mate.GetValueOrDefault();
		}
		set
		{
			mate = value;
		}
	}

	public bool HasNodes => nodes.HasValue;

	public long Nodes
	{
		get
		{
			return nodes.GetValueOrDefault();
		}
		set
		{
			nodes = value;
		}
	}

	public bool HasDepth => depth.HasValue;

	public int Depth
	{
		get
		{
			return depth.GetValueOrDefault();
		}
		set
		{
			depth = value;
		}
	}

	public bool HasSelDepth => selDepth.HasValue;

	public int SelDepth
	{
		get
		{
			return selDepth.GetValueOrDefault();
		}
		set
		{
			selDepth = value;
		}
	}

	public int Eval
	{
		get
		{
			if (!mate.HasValue || Mate == 0)
			{
				return Score;
			}
			int num = 32000 - Score;
			if (Mate < 0)
			{
				num = -num;
			}
			return num;
		}
	}

	public bool HasEval
	{
		get
		{
			if (!score.HasValue)
			{
				return mate.HasValue;
			}
			return true;
		}
	}

	public bool HasNPS => nps.HasValue;

	public int NPS
	{
		get
		{
			return nps.GetValueOrDefault();
		}
		set
		{
			nps = value;
		}
	}

	public List<MoveDataEx> PvMoves
	{
		get
		{
			return pvmoves_;
		}
		set
		{
			pvmoves_ = value;
		}
	}

	public string Message
	{
		get
		{
			return message;
		}
		set
		{
			message = value;
		}
	}

	public HashKey HashKey { get; set; }

	public int Bounds
	{
		get
		{
			return bounds;
		}
		set
		{
			bounds = value;
		}
	}

	public PvInfo()
	{
	}

	public PvInfo(int rank)
	{
		Rank = rank;
	}

	public PvInfo(int num, int timeMs, int score, int mate, long nodes, int depth, int seldepth, List<MoveDataEx> moves, HashKey hashkey)
	{
		Rank = num;
		TimeMs = timeMs;
		Score = score;
		Mate = mate;
		Nodes = nodes;
		Depth = depth;
		SelDepth = seldepth;
		pvmoves_ = moves;
		HashKey = hashkey;
	}

	public override string ToString()
	{
		return ToString(MoveStyle.Kif);
	}

	public string ToString(MoveStyle style)
	{
		string text = string.Empty;
		if (HasRank)
		{
			text += RankToString(Rank);
		}
		if (HasTimeMs)
		{
			text = text + " " + timeText + " " + TimeToString(TimeMs);
		}
		if (HasDepth)
		{
			text += $" {depthText} {Depth}/{SelDepth}";
		}
		if (HasNodes)
		{
			text += $" {nodesText} {Nodes}";
		}
		if (HasEval)
		{
			if (Settings.AppSettings.ConvertEvalToWinRate)
			{
				int.TryParse(Settings.AppSettings.WinRateCoefficient, out int coeffInt);
				double coeff = coeffInt > 0 ? coeffInt : WinRateUtil.DefaultCoefficient;
				text = text + " " + valueText + " " + WinRateUtil.FormatWinRate(Score, HasMate, Mate, coeff);
			}
			else
			{
				text = text + " " + valueText + " " + ValueToString(Mate, Score, 0);
			}
		}
		if (pvmoves_ != null && pvmoves_.Count != 0)
		{
			text = text + " " + movesText + " " + MovesToString(pvmoves_, style);
		}
		else if (message != null)
		{
			text = text + " " + stringText + " " + message;
		}
		return text;
	}

	public static string RankToString(int rank)
	{
		if (rank == 0)
		{
			return string.Empty;
		}
		return rank.ToString();
	}

	public static string TimeToString(int timeMs)
	{
		int num = timeMs / 60000;
		int num2 = (timeMs - num * 60 * 1000) / 1000;
		int num3 = (timeMs - num * 60 * 1000) % 1000;
		return $"{num:00}:{num2:00}.{num3 / 100}";
	}

	public static string ValueToString(int mate, int score, int bounds)
	{
		string empty = string.Empty;
		if (mate == 0)
		{
			empty = $"{score}";
			if (bounds > 0)
			{
				empty += "↑";
			}
			else if (bounds < 0)
			{
				empty += "↓";
			}
		}
		else
		{
			empty = ((mate < 0) ? "-" : "+");
			empty += mateText;
			if (score != 0)
			{
				empty = empty + " " + score;
			}
		}
		return empty;
	}

	public static string MovesToString(IList<MoveDataEx> moves, MoveStyle style)
	{
		string text = string.Empty;
		foreach (MoveDataEx move in moves)
		{
			text = text + " " + ((move.Turn == PlayerColor.Black) ? "☗" : "☖") + move.ToString(style);
		}
		return text;
	}

	/// <summary>
	/// 候補手（最初の1手）を返す。
	/// </summary>
	public string GetFirstMove(MoveStyle style)
	{
		if (PvMoves == null || PvMoves.Count == 0) return string.Empty;
		var m = PvMoves[0];
		return ((m.Turn == PlayerColor.Black) ? "☗" : "☖") + m.ToString(style);
	}

	/// <summary>
	/// 候補手以降の残りの読み筋を返す。
	/// </summary>
	public string GetRestMoves(MoveStyle style)
	{
		if (PvMoves == null || PvMoves.Count <= 1) return string.Empty;
		string text = string.Empty;
		for (int i = 1; i < PvMoves.Count; i++)
		{
			var m = PvMoves[i];
			text = text + " " + ((m.Turn == PlayerColor.Black) ? "☗" : "☖") + m.ToString(style);
		}
		return text.TrimStart();
	}

	private static string FormatWithSuffix(long value, long divisor, string suffix)
	{
		long whole = value / divisor;
		if (whole >= 100)
			return whole + suffix;
		if (whole >= 10)
			return whole + "." + (value / (divisor / 10) % 10) + suffix;
		return whole + "." + (value / (divisor / 100) % 100).ToString("D2") + suffix;
	}

	public static string NodesToString(long nodes)
	{
		if (nodes >= 1000000000L)
			return FormatWithSuffix(nodes, 1000000000L, "B");
		if (nodes >= 1000000)
			return FormatWithSuffix(nodes, 1000000, "M");
		if (nodes >= 1000)
			return FormatWithSuffix(nodes, 1000, "k");
		return nodes.ToString();
	}

	public static string NpsToString(long nodes)
	{
		if (nodes >= 1000000)
			return FormatWithSuffix(nodes, 1000000, "M");
		if (nodes >= 1000)
			return FormatWithSuffix(nodes, 1000, "k");
		return nodes.ToString();
	}

	public string GetMoves(MoveStyle moveStyle)
	{
		string result = string.Empty;
		if (PvMoves != null && PvMoves.Count != 0)
		{
			result = MovesToString(PvMoves, moveStyle);
		}
		else if (Message != null)
		{
			result = Message;
		}
		return result;
	}
}
