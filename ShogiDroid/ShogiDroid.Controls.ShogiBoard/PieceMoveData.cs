using ShogiLib;

namespace ShogiDroid.Controls.ShogiBoard;

public class PieceMoveData : MoveData
{
	public MoveDataDir Dir;

	public MoveData CurrentMoveData;

	public PieceMoveData(MoveDataDir dir, MoveData moveData)
		: base(moveData)
	{
		Dir = dir;
		CurrentMoveData = moveData;
	}

	public PieceMoveData(MoveDataDir dir, MoveData moveData, MoveData current)
		: base(moveData)
	{
		Dir = dir;
		CurrentMoveData = new MoveData(current);
	}
}
