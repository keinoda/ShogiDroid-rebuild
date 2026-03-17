namespace ShogiLib;

public static class MoveTypeExtentions
{
	public static bool IsResult(this MoveType type)
	{
		bool result = false;
		if (type.HasFlag(MoveType.ResultFlag))
		{
			result = true;
		}
		return result;
	}

	public static bool IsMove(this MoveType type)
	{
		bool result = false;
		if (type.HasFlag(MoveType.MoveFlag) || type.HasFlag(MoveType.DropFlag))
		{
			result = true;
		}
		return result;
	}

	public static bool IsMoveWithoutPass(this MoveType type)
	{
		bool result = false;
		if (type != MoveType.Pass && (type.HasFlag(MoveType.MoveFlag) || type.HasFlag(MoveType.DropFlag)))
		{
			result = true;
		}
		return result;
	}
}
