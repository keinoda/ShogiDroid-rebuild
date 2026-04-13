namespace ShogiLib;

public static class MoveTypeExtensions
{
	public static bool IsResult(this MoveType type)
	{
		return type.HasFlag(MoveType.ResultFlag);
	}

	public static bool IsMove(this MoveType type)
	{
		return type.HasFlag(MoveType.MoveFlag) || type.HasFlag(MoveType.DropFlag);
	}

	public static bool IsMoveWithoutPass(this MoveType type)
	{
		return type != MoveType.Pass && (type.HasFlag(MoveType.MoveFlag) || type.HasFlag(MoveType.DropFlag));
	}
}
