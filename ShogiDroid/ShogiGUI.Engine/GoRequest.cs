using ShogiLib;

namespace ShogiGUI.Engine;

public class GoRequest
{
	public enum Type
	{
		NORMAL,
		TIME_INFINITY,
		PONDER,
		MATE,
		MOVETIME
	}

	private SPosition pos_;

	private string sfen_ = string.Empty;

	private string moves_ = string.Empty;

	public int TransactionNo;

	public Type ReqType { get; set; }

	public int Btime { get; set; }

	public int Wtime { get; set; }

	public int Byoyomi { get; set; }

	public long Nodes { get; set; }

	public long Depth { get; set; }

	public long Time { get; set; }

	public string Sfen
	{
		get
		{
			return sfen_;
		}
		set
		{
			sfen_ = value;
		}
	}

	public string Moves
	{
		get
		{
			return moves_;
		}
		set
		{
			moves_ = value;
		}
	}

	public SPosition Pos
	{
		get
		{
			return pos_;
		}
		set
		{
			pos_ = value;
		}
	}

	public GoRequest()
	{
		ReqType = Type.TIME_INFINITY;
	}

	public GoRequest(int btime, int wtime, int byoyomi)
	{
		ReqType = Type.NORMAL;
		Btime = btime;
		Wtime = wtime;
		Byoyomi = byoyomi;
	}

	public GoRequest(Type type)
	{
		ReqType = type;
	}

	public GoRequest(Type type, int movetime)
	{
		ReqType = type;
		Time = movetime;
	}

	public GoRequest(AnalyzeTimeSettings settings)
	{
		ReqType = Type.MOVETIME;
		Nodes = settings.Nodes;
		Depth = settings.Depth;
		Time = settings.Time;
	}
}
