using System;

namespace ShogiLib;

public static class MoveCheck
{
	private static readonly MoveCoord[] FuMoveOfs = new MoveCoord[1]
	{
		new MoveCoord(-1, 0)
	};

	private static readonly MoveCoord[] KeiMoveOfs = new MoveCoord[2]
	{
		new MoveCoord(-2, 1),
		new MoveCoord(-2, -1)
	};

	private static readonly MoveCoord[] GinMoveOfs = new MoveCoord[5]
	{
		new MoveCoord(-1, -1),
		new MoveCoord(-1, 0),
		new MoveCoord(-1, 1),
		new MoveCoord(1, -1),
		new MoveCoord(1, 1)
	};

	private static readonly MoveCoord[] KinMoveOfs = new MoveCoord[6]
	{
		new MoveCoord(-1, -1),
		new MoveCoord(-1, 0),
		new MoveCoord(-1, 1),
		new MoveCoord(0, -1),
		new MoveCoord(0, 1),
		new MoveCoord(1, 0)
	};

	private static readonly MoveCoord[] GyokuMoveOfs = new MoveCoord[8]
	{
		new MoveCoord(-1, -1),
		new MoveCoord(-1, 0),
		new MoveCoord(-1, 1),
		new MoveCoord(0, -1),
		new MoveCoord(0, 1),
		new MoveCoord(1, -1),
		new MoveCoord(1, 0),
		new MoveCoord(1, 1)
	};

	public static bool IsValid(SPosition pos, MoveData move_data)
	{
		return IsValid(pos, move_data, light: false);
	}

	public static bool IsValidLight(SPosition pos, MoveData move_data)
	{
		return IsValid(pos, move_data, light: true);
	}

	public static bool IsValid(SPosition pos, MoveData move_data, bool light)
	{
		Piece piece = move_data.Piece;
		if (move_data.MoveType == MoveType.Pass)
		{
			return true;
		}
		if (piece == Piece.NoPiece)
		{
			return false;
		}
		if (pos.Turn != piece.ColorOf())
		{
			return false;
		}
		if (!Square.InBoard(move_data.ToSquare))
		{
			return false;
		}
		if (!Square.InBoard(move_data.FromSquare))
		{
			return false;
		}
		if (!move_data.MoveType.IsMove())
		{
			return false;
		}
		if (move_data.MoveType.HasFlag(MoveType.DropFlag))
		{
			if (!pos.IsHand(pos.Turn, piece.ToHandIndex()))
			{
				return false;
			}
			if (pos.GetPiece(move_data.ToSquare) != Piece.NoPiece)
			{
				return false;
			}
			switch (piece)
			{
			case Piece.BFU:
			case Piece.BKYO:
				if (move_data.ToSquare.RankOf() <= 0)
				{
					return false;
				}
				break;
			case Piece.WFU:
			case Piece.WKYO:
				if (move_data.ToSquare.RankOf() >= 8)
				{
					return false;
				}
				break;
			case Piece.BKEI:
				if (move_data.ToSquare.RankOf() <= 1)
				{
					return false;
				}
				break;
			case Piece.WKEI:
				if (move_data.ToSquare.RankOf() >= 7)
				{
					return false;
				}
				break;
			}
			if (piece == Piece.BFU || piece == Piece.WFU)
			{
				if (PieceCountOfFile(pos, piece, move_data.ToSquare.FileOf()) >= 1)
				{
					return false;
				}
				if (light)
				{
					return true;
				}
				if (pos.Turn == PlayerColor.White)
				{
					if (IsMateWhite1(pos, move_data))
					{
						return false;
					}
				}
				else if (IsMateBlack1(pos, move_data))
				{
					return false;
				}
			}
		}
		else
		{
			if (pos.GetPiece(move_data.FromSquare) != piece)
			{
				return false;
			}
			Piece piece2 = pos.GetPiece(move_data.ToSquare);
			if (piece2 != Piece.NoPiece && pos.Turn == piece2.ColorOf())
			{
				return false;
			}
			bool flag = false;
			int num = move_data.ToSquare.RankOf() - move_data.FromSquare.RankOf();
			int num2 = move_data.ToSquare.FileOf() - move_data.FromSquare.FileOf();
			switch (piece)
			{
			case Piece.BFU:
				flag = CanMove(FuMoveOfs, num2, num);
				break;
			case Piece.WFU:
				flag = CanMove(FuMoveOfs, -num2, -num);
				break;
			case Piece.BKYO:
				flag = num < 0 && CanMoveRank(pos, move_data);
				break;
			case Piece.WKYO:
				flag = num > 0 && CanMoveRank(pos, move_data);
				break;
			case Piece.BKEI:
				flag = CanMove(KeiMoveOfs, num2, num);
				break;
			case Piece.WKEI:
				flag = CanMove(KeiMoveOfs, -num2, -num);
				break;
			case Piece.BGIN:
				flag = CanMove(GinMoveOfs, num2, num);
				break;
			case Piece.WGIN:
				flag = CanMove(GinMoveOfs, -num2, -num);
				break;
			case Piece.BKAK:
			case Piece.WKAK:
				flag = CanMoveDiagonal(pos, move_data);
				break;
			case Piece.BUMA:
			case Piece.WUMA:
				flag = ((pos.Turn != PlayerColor.White) ? CanMove(GyokuMoveOfs, num2, num) : CanMove(GyokuMoveOfs, -num2, -num));
				if (!flag)
				{
					flag = CanMoveDiagonal(pos, move_data);
				}
				break;
			case Piece.BHI:
			case Piece.WHI:
				flag = CanMoveRank(pos, move_data);
				if (!flag)
				{
					flag = CanMoveFile(pos, move_data);
				}
				break;
			case Piece.BRYU:
			case Piece.WRYU:
				flag = ((pos.Turn != PlayerColor.White) ? CanMove(GyokuMoveOfs, num2, num) : CanMove(GyokuMoveOfs, -num2, -num));
				if (!flag)
				{
					flag = CanMoveRank(pos, move_data);
					if (!flag)
					{
						flag = CanMoveFile(pos, move_data);
					}
				}
				break;
			case Piece.BOU:
				flag = CanMove(GyokuMoveOfs, num2, num);
				break;
			case Piece.WOU:
				flag = CanMove(GyokuMoveOfs, -num2, -num);
				break;
			default:
				flag = ((pos.Turn != PlayerColor.White) ? CanMove(KinMoveOfs, num2, num) : CanMove(KinMoveOfs, -num2, -num));
				break;
			}
			if (!flag)
			{
				return false;
			}
		}
		if (light)
		{
			return true;
		}
		SPosition sPosition = (SPosition)pos.Clone();
		if (sPosition.Move(move_data))
		{
			if (pos.Turn == PlayerColor.White)
			{
				if (IsMateLeftWhite(sPosition))
				{
					return false;
				}
			}
			else if (IsMateLeftBlack(sPosition))
			{
				return false;
			}
		}
		return true;
	}

	private static bool CanMove(MoveCoord[] move_ofs, int file_ofs, int rank_ofs)
	{
		for (int i = 0; i < move_ofs.Length; i++)
		{
			MoveCoord moveCoord = move_ofs[i];
			if (moveCoord.Rank == rank_ofs && moveCoord.File == file_ofs)
			{
				return true;
			}
		}
		return false;
	}

	private static bool CanMoveRank(SPosition pos, MoveData move_data)
	{
		if (move_data.ToSquare.FileOf() != move_data.FromSquare.FileOf())
		{
			return false;
		}
		int num = ((move_data.ToSquare <= move_data.FromSquare) ? (-9) : 9);
		for (int i = move_data.FromSquare + num; i != move_data.ToSquare; i += num)
		{
			if (pos.GetPiece(i) != Piece.NoPiece)
			{
				return false;
			}
		}
		return true;
	}

	private static bool CanMoveFile(SPosition pos, MoveData move_data)
	{
		if (move_data.ToSquare.RankOf() != move_data.FromSquare.RankOf())
		{
			return false;
		}
		int num = ((move_data.ToSquare > move_data.FromSquare) ? 1 : (-1));
		for (int i = move_data.FromSquare + num; i != move_data.ToSquare; i += num)
		{
			if (pos.GetPiece(i) != Piece.NoPiece)
			{
				return false;
			}
		}
		return true;
	}

	private static bool CanMoveDiagonal(SPosition pos, MoveData move_data)
	{
		int num = move_data.ToSquare.FileOf() - move_data.FromSquare.FileOf();
		int num2 = move_data.ToSquare.RankOf() - move_data.FromSquare.RankOf();
		if (Math.Abs(num) != Math.Abs(num2))
		{
			return false;
		}
		num2 /= Math.Abs(num2);
		num /= Math.Abs(num);
		int num3 = num2 * 9 + num;
		for (int i = move_data.FromSquare + num3; move_data.ToSquare != i; i += num3)
		{
			if (!Square.InBoard(i))
			{
				return false;
			}
			if (pos.GetPiece(i) != Piece.NoPiece)
			{
				return false;
			}
		}
		return true;
	}

	private static int PieceCountOfFile(SPosition pos, Piece piece, int file)
	{
		int num = 0;
		for (int i = 0; i < 9; i++)
		{
			if (pos.GetPiece(file, i) == piece)
			{
				num++;
			}
		}
		return num;
	}

	private static void ClearEffectData(int[] effect_data)
	{
		for (int i = 0; i < 81; i++)
		{
			effect_data[i] = 0;
		}
	}

	private static bool IsMateBlack1(SPosition pos, MoveData move_data)
	{
		SPosition sPosition = (SPosition)pos.Clone();
		int[] array = new int[81];
		ClearEffectData(array);
		if (!sPosition.Move(move_data))
		{
			return false;
		}
		if (!IsCheckBlack(sPosition, move_data.ToSquare))
		{
			return false;
		}
		MakeEffectBlack(sPosition, array, Piece.NoPiece);
		if (array[move_data.ToSquare] == 0)
		{
			return false;
		}
		int sq;
		if ((sq = sPosition.SearchPiece(Piece.WOU)) == 81)
		{
			return false;
		}
		int num = sq.RankOf();
		int num2 = sq.FileOf();
		MoveCoord[] gyokuMoveOfs = GyokuMoveOfs;
		for (int i = 0; i < gyokuMoveOfs.Length; i++)
		{
			MoveCoord moveCoord = gyokuMoveOfs[i];
			int rank = num - moveCoord.Rank;
			int file = num2 - moveCoord.File;
			if (SPosition.InBoard(file, rank) && !sPosition.IsWhite(file, rank) && array[Square.Make(file, rank)] == 0)
			{
				return false;
			}
		}
		if (responseWhite(sPosition, move_data.ToSquare, Piece.WOU))
		{
			return false;
		}
		return true;
	}

	private static bool IsMateWhite1(SPosition pos, MoveData move_data)
	{
		SPosition sPosition = (SPosition)pos.Clone();
		int[] array = new int[81];
		ClearEffectData(array);
		if (!sPosition.Move(move_data))
		{
			return false;
		}
		if (!IsCheckWhite(sPosition, move_data.ToSquare))
		{
			return false;
		}
		MakeEffectWhite(sPosition, array, Piece.NoPiece);
		if (array[move_data.ToSquare] == 0)
		{
			return false;
		}
		int sq;
		if ((sq = sPosition.SearchPiece(Piece.BOU)) == 81)
		{
			return false;
		}
		int num = sq.RankOf();
		int num2 = sq.FileOf();
		MoveCoord[] gyokuMoveOfs = GyokuMoveOfs;
		for (int i = 0; i < gyokuMoveOfs.Length; i++)
		{
			MoveCoord moveCoord = gyokuMoveOfs[i];
			int rank = num + moveCoord.Rank;
			int file = num2 + moveCoord.File;
			if (SPosition.InBoard(file, rank) && !sPosition.IsBlack(file, rank) && array[Square.Make(file, rank)] == 0)
			{
				return false;
			}
		}
		if (responseBlack(sPosition, move_data.ToSquare, Piece.BOU))
		{
			return false;
		}
		return true;
	}

	private static bool IsCheckBlack(SPosition pos, int square)
	{
		pos.GetPiece(square);
		int[] array = new int[81];
		ClearEffectData(array);
		MakeEfectBlackOne(pos, square, array);
		int num = pos.SearchPiece(Piece.WOU);
		if (num < 81 && array[num] != 0)
		{
			return true;
		}
		return false;
	}

	private static bool IsCheckWhite(SPosition pos, int square)
	{
		pos.GetPiece(square);
		int[] array = new int[81];
		ClearEffectData(array);
		MakeEfectWhiteOne(pos, square, array);
		int num = pos.SearchPiece(Piece.BOU);
		if (num < 81 && array[num] != 0)
		{
			return true;
		}
		return false;
	}

	private static bool responseWhite(SPosition pos, int to_sq, Piece skip_piece)
	{
		int[] array = new int[81];
		Piece piece = pos.GetPiece(to_sq);
		for (int i = 0; i < 81; i++)
		{
			Piece piece2 = pos.GetPiece(i);
			if (piece2.ColorOf() != PlayerColor.White || skip_piece == piece2)
			{
				continue;
			}
			ClearEffectData(array);
			MakeEfectWhiteOne(pos, i, array);
			if (array[to_sq] != 0)
			{
				MoveData moveData = new MoveData(MoveType.MoveFlag, i, to_sq, piece2, piece);
				pos.Move(moveData);
				if (!IsMateLeftWhite(pos))
				{
					return true;
				}
				pos.UnMove(moveData, null);
			}
		}
		return false;
	}

	private static bool responseBlack(SPosition pos, int to_sq, Piece skip_piece)
	{
		int[] array = new int[81];
		Piece piece = pos.GetPiece(to_sq);
		for (int i = 0; i < 81; i++)
		{
			Piece piece2 = pos.GetPiece(i);
			if (piece2.ColorOf() != PlayerColor.Black || skip_piece == piece2)
			{
				continue;
			}
			ClearEffectData(array);
			MakeEfectBlackOne(pos, i, array);
			if (array[to_sq] != 0)
			{
				MoveData moveData = new MoveData(MoveType.MoveFlag, i, to_sq, piece2, piece);
				pos.Move(moveData);
				if (!IsMateLeftBlack(pos))
				{
					return true;
				}
				pos.UnMove(moveData, null);
			}
		}
		return false;
	}

	private static void MakeEffectBlack(SPosition pos, int[] effect_data, Piece skip_piece)
	{
		for (int i = 0; i < 81; i++)
		{
			Piece piece = pos.GetPiece(i);
			if (piece.ColorOf() == PlayerColor.Black && piece != skip_piece)
			{
				MakeEfectBlackOne(pos, i, effect_data);
			}
		}
	}

	private static void MakeEffectWhite(SPosition pos, int[] effect_data, Piece skip_piece)
	{
		for (int i = 0; i < 81; i++)
		{
			Piece piece = pos.GetPiece(i);
			if (piece.ColorOf() == PlayerColor.White && skip_piece != piece)
			{
				MakeEfectWhiteOne(pos, i, effect_data);
			}
		}
	}

	private static void MakeEfectBlackOne(SPosition pos, int square, int[] effect_data)
	{
		Piece piece = pos.GetPiece(square);
		int num = square.RankOf();
		int num2 = square.FileOf();
		switch (piece)
		{
		case Piece.BFU:
			MakeEffectOne(PlayerColor.Black, effect_data, FuMoveOfs, num2, num);
			break;
		case Piece.BKYO:
			MakeEffect(pos, effect_data, num2, num, 0, -1);
			break;
		case Piece.BKEI:
			MakeEffectOne(PlayerColor.Black, effect_data, KeiMoveOfs, num2, num);
			break;
		case Piece.BGIN:
			MakeEffectOne(PlayerColor.Black, effect_data, GinMoveOfs, num2, num);
			break;
		case Piece.BKAK:
			MakeEffect(pos, effect_data, num2, num, -1, -1);
			MakeEffect(pos, effect_data, num2, num, 1, -1);
			MakeEffect(pos, effect_data, num2, num, -1, 1);
			MakeEffect(pos, effect_data, num2, num, 1, 1);
			break;
		case Piece.BUMA:
			MakeEffectOne(PlayerColor.Black, effect_data, GyokuMoveOfs, num2, num);
			MakeEffect(pos, effect_data, num2, num, -1, -1);
			MakeEffect(pos, effect_data, num2, num, 1, -1);
			MakeEffect(pos, effect_data, num2, num, -1, 1);
			MakeEffect(pos, effect_data, num2, num, 1, 1);
			break;
		case Piece.BHI:
			MakeEffect(pos, effect_data, num2, num, 0, -1);
			MakeEffect(pos, effect_data, num2, num, 0, 1);
			MakeEffect(pos, effect_data, num2, num, -1, 0);
			MakeEffect(pos, effect_data, num2, num, 1, 0);
			break;
		case Piece.BRYU:
			MakeEffectOne(PlayerColor.Black, effect_data, GyokuMoveOfs, num2, num);
			MakeEffect(pos, effect_data, num2, num, 0, -1);
			MakeEffect(pos, effect_data, num2, num, 0, 1);
			MakeEffect(pos, effect_data, num2, num, -1, 0);
			MakeEffect(pos, effect_data, num2, num, 1, 0);
			break;
		case Piece.BOU:
			MakeEffectOne(PlayerColor.Black, effect_data, GyokuMoveOfs, num2, num);
			break;
		default:
			MakeEffectOne(PlayerColor.Black, effect_data, KinMoveOfs, num2, num);
			break;
		}
	}

	private static void MakeEfectWhiteOne(SPosition pos, int square, int[] effect_data)
	{
		Piece piece = pos.GetPiece(square);
		int num = square.RankOf();
		int num2 = square.FileOf();
		switch (piece)
		{
		case Piece.WFU:
			MakeEffectOne(PlayerColor.White, effect_data, FuMoveOfs, num2, num);
			break;
		case Piece.WKYO:
			MakeEffect(pos, effect_data, num2, num, 0, 1);
			break;
		case Piece.WKEI:
			MakeEffectOne(PlayerColor.White, effect_data, KeiMoveOfs, num2, num);
			break;
		case Piece.WGIN:
			MakeEffectOne(PlayerColor.White, effect_data, GinMoveOfs, num2, num);
			break;
		case Piece.WKAK:
			MakeEffect(pos, effect_data, num2, num, -1, -1);
			MakeEffect(pos, effect_data, num2, num, 1, -1);
			MakeEffect(pos, effect_data, num2, num, -1, 1);
			MakeEffect(pos, effect_data, num2, num, 1, 1);
			break;
		case Piece.WUMA:
			MakeEffectOne(PlayerColor.White, effect_data, GyokuMoveOfs, num2, num);
			MakeEffect(pos, effect_data, num2, num, -1, -1);
			MakeEffect(pos, effect_data, num2, num, 1, -1);
			MakeEffect(pos, effect_data, num2, num, -1, 1);
			MakeEffect(pos, effect_data, num2, num, 1, 1);
			break;
		case Piece.WHI:
			MakeEffect(pos, effect_data, num2, num, -1, 0);
			MakeEffect(pos, effect_data, num2, num, 1, 0);
			MakeEffect(pos, effect_data, num2, num, 0, -1);
			MakeEffect(pos, effect_data, num2, num, 0, 1);
			break;
		case Piece.WRYU:
			MakeEffectOne(PlayerColor.White, effect_data, GyokuMoveOfs, num2, num);
			MakeEffect(pos, effect_data, num2, num, -1, 0);
			MakeEffect(pos, effect_data, num2, num, 1, 0);
			MakeEffect(pos, effect_data, num2, num, 0, -1);
			MakeEffect(pos, effect_data, num2, num, 0, 1);
			break;
		case Piece.WOU:
			MakeEffectOne(PlayerColor.White, effect_data, GyokuMoveOfs, num2, num);
			break;
		default:
			MakeEffectOne(PlayerColor.White, effect_data, KinMoveOfs, num2, num);
			break;
		}
	}

	private static void MakeEffectOne(PlayerColor turn, int[] effect_data, MoveCoord[] move_ofs, int move_to_file, int move_to_rank)
	{
		for (int i = 0; i < move_ofs.Length; i++)
		{
			MoveCoord moveCoord = move_ofs[i];
			int rank;
			int file;
			if (turn == PlayerColor.Black)
			{
				rank = move_to_rank + moveCoord.Rank;
				file = move_to_file + moveCoord.File;
			}
			else
			{
				rank = move_to_rank - moveCoord.Rank;
				file = move_to_file - moveCoord.File;
			}
			if (SPosition.InBoard(file, rank))
			{
				effect_data[Square.Make(file, rank)]++;
			}
		}
	}

	private static void MakeEffect(SPosition pos, int[] effect_data, int file, int rank, int file_ofs, int rank_ofs)
	{
		for (int i = 0; i < 9; i++)
		{
			rank += rank_ofs;
			file += file_ofs;
			if (SPosition.InBoard(file, rank))
			{
				int num = Square.Make(file, rank);
				effect_data[num]++;
				if (pos.GetPiece(num) != Piece.NoPiece)
				{
					break;
				}
				continue;
			}
			break;
		}
	}

	private static bool IsMateLeftBlack(SPosition pos)
	{
		int[] array = new int[81];
		ClearEffectData(array);
		int num;
		if ((num = pos.SearchPiece(Piece.BOU)) == 81)
		{
			return false;
		}
		MakeEffectWhite(pos, array, Piece.NoPiece);
		if (array[num] == 0)
		{
			return false;
		}
		return true;
	}

	private static bool IsMateLeftWhite(SPosition pos)
	{
		int[] array = new int[81];
		ClearEffectData(array);
		int num;
		if ((num = pos.SearchPiece(Piece.WOU)) == 81)
		{
			return false;
		}
		MakeEffectBlack(pos, array, Piece.NoPiece);
		if (array[num] == 0)
		{
			return false;
		}
		return true;
	}

	public static bool CanPromota(MoveData move_data)
	{
		bool result = false;
		Piece piece = move_data.Piece;
		if (!move_data.MoveType.HasFlag(MoveType.MoveFlag))
		{
			return false;
		}
		if (piece.IsPromoted())
		{
			return false;
		}
		if (piece.TypeOf() == PieceType.KIN || piece.TypeOf() == PieceType.OU)
		{
			return false;
		}
		if (piece.ColorOf() == PlayerColor.Black)
		{
			if (move_data.ToSquare.RankOf() <= 2 || move_data.FromSquare.RankOf() <= 2)
			{
				result = true;
			}
		}
		else if (move_data.ToSquare.RankOf() >= 6 || move_data.FromSquare.RankOf() >= 6)
		{
			result = true;
		}
		return result;
	}

	public static bool ForcePromotion(Piece piece, int square)
	{
		switch (piece)
		{
		case Piece.BFU:
		case Piece.BKYO:
			if (square.RankOf() <= 0)
			{
				return true;
			}
			break;
		case Piece.WFU:
		case Piece.WKYO:
			if (square.RankOf() >= 8)
			{
				return true;
			}
			break;
		case Piece.BKEI:
			if (square.RankOf() <= 1)
			{
				return true;
			}
			break;
		case Piece.WKEI:
			if (square.RankOf() >= 7)
			{
				return true;
			}
			break;
		}
		return false;
	}

	public static bool IsCheck(SPosition pos, MoveData moveData)
	{
		bool result = false;
		if (moveData.MoveType == MoveType.Pass)
		{
			return false;
		}
		if (pos.Turn == PlayerColor.White)
		{
			if (IsCheckBlack(pos, moveData.ToSquare))
			{
				result = true;
			}
		}
		else if (IsCheckWhite(pos, moveData.ToSquare))
		{
			result = true;
		}
		return result;
	}

	public static bool IsCheck(SPosition pos)
	{
		bool flag = false;
		if (pos.Turn == PlayerColor.White)
		{
			return IsMateLeftWhite(pos);
		}
		return IsMateLeftBlack(pos);
	}
}
