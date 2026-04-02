using System.Collections.Generic;
using ShogiLib;

namespace ShogiGUI.Engine;

public enum PolicyState { None, Analyzing, Done, Error }

public class PolicyMoveInfo
{
	public int Rank { get; set; }
	public string MoveUSI { get; set; }
	public double SelectionRate { get; set; } // 0.0 - 100.0%
}

public class PolicyInfo
{
	public PolicyState State { get; set; }
	public List<PolicyMoveInfo> Moves { get; set; } = new List<PolicyMoveInfo>();

	public static PolicyInfo None() => new PolicyInfo { State = PolicyState.None };
}
