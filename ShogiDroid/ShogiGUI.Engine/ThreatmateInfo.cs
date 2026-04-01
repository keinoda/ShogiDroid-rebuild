using System.Collections.Generic;
using ShogiLib;

namespace ShogiGUI.Engine;

public enum ThreatmateState
{
	None,
	Analyzing,
	Threatmate,
	NoThreatmate,
	Unknown
}

public class ThreatmateInfo
{
	public ThreatmateState State { get; set; }

	public PlayerColor Attacker { get; set; }

	public int MatePly { get; set; }

	public List<MoveDataEx> Moves { get; set; }

	public bool HasThreatmate => State == ThreatmateState.Threatmate;

	public bool IsAnalyzing => State == ThreatmateState.Analyzing;

	public bool HasMoves => Moves != null && Moves.Count != 0;

	public ThreatmateInfo Clone()
	{
		return new ThreatmateInfo
		{
			State = State,
			Attacker = Attacker,
			MatePly = MatePly,
			Moves = CloneMoves(Moves)
		};
	}

	public static ThreatmateInfo None()
	{
		return new ThreatmateInfo
		{
			State = ThreatmateState.None,
			Attacker = PlayerColor.NoColor,
			MatePly = 0,
			Moves = new List<MoveDataEx>()
		};
	}

	public static List<MoveDataEx> CloneMoves(IList<MoveDataEx> moves)
	{
		List<MoveDataEx> cloned = new List<MoveDataEx>();
		if (moves == null)
		{
			return cloned;
		}
		foreach (MoveDataEx move in moves)
		{
			cloned.Add(new MoveDataEx(move));
		}
		return cloned;
	}
}
