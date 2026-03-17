using System.Collections.Generic;

namespace ShogiLib;

public static class MoveActionExtention
{
	private static readonly string[] DisplayStrings = new string[14]
	{
		string.Empty,
		"右",
		"左",
		"上",
		"引",
		"寄",
		"直",
		"右上",
		"右引",
		"左上",
		"左引",
		"右寄",
		"左寄",
		"打"
	};

	public static string DisplayName(this MoveAction action)
	{
		return DisplayStrings[(int)action];
	}

	public static MoveAction GetAction(this MoveData move_data, SPosition pos)
	{
		MoveAction result = MoveAction.None;
		if (!move_data.MoveType.IsMove())
		{
			return MoveAction.None;
		}
		if (move_data.MoveType.HasFlag(MoveType.DropFlag))
		{
			if (CanSamePlaceMove(pos, move_data))
			{
				return MoveAction.Drop;
			}
			return MoveAction.None;
		}
		MoveData moveData = new MoveData(move_data);
		List<int> list = new List<int>();
		for (int i = 0; i < 81; i++)
		{
			if (i != move_data.FromSquare && pos.GetPiece(i) == move_data.Piece)
			{
				moveData.FromSquare = i;
				if (MoveCheck.IsValidLight(pos, moveData))
				{
					list.Add(i);
				}
			}
		}
		if (list.Count != 0)
		{
			if (move_data.FromSquare.RankOf() == move_data.ToSquare.RankOf())
			{
				result = (IsRankEmpty(list, move_data.FromSquare) ? MoveAction.Sideways : (IsRightMost(list, move_data.FromSquare) ? ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.RightSideways : MoveAction.LeftSideways)) : ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn != PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.LeftSideways : MoveAction.RightSideways))));
			}
			else if (move_data.FromSquare.RankOf() > move_data.ToSquare.RankOf())
			{
				result = (IsRankGrater(list, move_data.ToSquare) ? ((pos.Turn == PlayerColor.Black) ? MoveAction.Forward : MoveAction.Backward) : ((move_data.FromSquare.FileOf() == move_data.ToSquare.FileOf()) ? ((move_data.Piece.TypeOf() != PieceType.HI && move_data.Piece.TypeOf() != PieceType.KAK) ? ((pos.Turn == PlayerColor.Black) ? MoveAction.Upright : MoveAction.Backward) : ((list[0].FileOf() >= move_data.FromSquare.FileOf()) ? ((pos.Turn != PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.Right : MoveAction.Left))) : (IsRightMost(list, move_data.FromSquare) ? ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.RightForward : MoveAction.LeftBackward)) : ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn != PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.LeftForward : MoveAction.RightBackward)))));
			}
			else if (move_data.FromSquare.RankOf() < move_data.ToSquare.RankOf())
			{
				result = (IsRankSmall(list, move_data.ToSquare) ? ((pos.Turn == PlayerColor.Black) ? MoveAction.Backward : MoveAction.Forward) : ((move_data.FromSquare.FileOf() == move_data.ToSquare.FileOf()) ? ((move_data.Piece.TypeOf() != PieceType.HI && move_data.Piece.TypeOf() != PieceType.KAK) ? ((pos.Turn == PlayerColor.Black) ? MoveAction.Backward : MoveAction.Upright) : ((list[0].FileOf() >= move_data.FromSquare.FileOf()) ? ((pos.Turn != PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.Right : MoveAction.Left))) : (IsRightMost(list, move_data.FromSquare) ? ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn == PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.RightBackward : MoveAction.LeftForward)) : ((!EqualFile(list, move_data.FromSquare)) ? ((pos.Turn != PlayerColor.Black) ? MoveAction.Right : MoveAction.Left) : ((pos.Turn == PlayerColor.Black) ? MoveAction.LeftBackward : MoveAction.RightForward)))));
			}
		}
		return result;
	}

	private static bool CanSamePlaceMove(SPosition pos, MoveData move_data)
	{
		MoveData moveData = new MoveData(move_data);
		moveData.MoveType &= (MoveType)191;
		moveData.MoveType |= MoveType.MoveFlag;
		for (int i = 0; i < 81; i++)
		{
			if (pos.GetPiece(i) == move_data.Piece)
			{
				moveData.FromSquare = i;
				if (MoveCheck.IsValidLight(pos, moveData))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsRankSmall(List<int> squareCollection, int sq)
	{
		bool result = true;
		foreach (int item in squareCollection)
		{
			if (item.RankOf() < sq.RankOf())
			{
				result = false;
				break;
			}
		}
		return result;
	}

	private static bool IsRankGrater(List<int> squareCollection, int sq)
	{
		bool result = true;
		foreach (int item in squareCollection)
		{
			if (item.RankOf() > sq.RankOf())
			{
				result = false;
				break;
			}
		}
		return result;
	}

	private static bool IsRankEmpty(List<int> squareCollection, int sq)
	{
		bool result = true;
		foreach (int item in squareCollection)
		{
			if (item.RankOf() == sq.RankOf())
			{
				result = false;
				break;
			}
		}
		return result;
	}

	private static bool IsRightMost(List<int> squareCollection, int sq)
	{
		bool result = false;
		foreach (int item in squareCollection)
		{
			if (item.FileOf() < sq.FileOf())
			{
				result = true;
				break;
			}
		}
		return result;
	}

	private static bool EqualFile(List<int> squareCollection, int sq)
	{
		bool result = false;
		foreach (int item in squareCollection)
		{
			if (item.FileOf() == sq.FileOf())
			{
				result = true;
				break;
			}
		}
		return result;
	}

	private static bool IsLeftMost(List<int> squareCollection, int sq)
	{
		bool result = false;
		foreach (int item in squareCollection)
		{
			if (item.FileOf() > sq.FileOf())
			{
				result = true;
				break;
			}
		}
		return result;
	}

	public static bool IsNotPromotion(this MoveData moveData)
	{
		if (moveData.MoveType.HasFlag(MoveType.MoveMask) || moveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			return false;
		}
		return MoveCheck.CanPromota(moveData);
	}
}
