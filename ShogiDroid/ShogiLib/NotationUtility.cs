namespace ShogiLib;

public static class NotationUtility
{
	private struct MoveCheckData
	{
		public int Rank;

		public int File;

		public int Enable;
	}

	private static readonly int[,] GinAbsFrom = new int[9, 2]
	{
		{ -1, -1 },
		{ 0, 0 },
		{ -1, 1 },
		{ 0, 0 },
		{ 0, 0 },
		{ 0, 0 },
		{ 1, -1 },
		{ 1, 0 },
		{ 1, 1 }
	};

	private static readonly int[,] KinAbsFrom = new int[9, 2]
	{
		{ 0, 0 },
		{ -1, 0 },
		{ 0, 0 },
		{ 0, -1 },
		{ 0, 0 },
		{ 0, 1 },
		{ 1, -1 },
		{ 1, 0 },
		{ 1, 1 }
	};

	private static readonly int[,] UmaAbsFrom = new int[9, 2]
	{
		{ 0, 0 },
		{ -1, 0 },
		{ 0, 0 },
		{ 0, -1 },
		{ 0, 0 },
		{ 0, 1 },
		{ 0, 0 },
		{ 1, 0 },
		{ 0, 0 }
	};

	private static readonly int[,] RyuAbsFrom = new int[9, 2]
	{
		{ -1, -1 },
		{ 0, 0 },
		{ -1, 1 },
		{ 0, 0 },
		{ 0, 0 },
		{ 0, 0 },
		{ 1, -1 },
		{ 0, 0 },
		{ 1, 1 }
	};

	private static readonly int[,] GyokuAbsFrom = new int[9, 2]
	{
		{ -1, -1 },
		{ -1, 0 },
		{ -1, 1 },
		{ 0, -1 },
		{ 0, 0 },
		{ 0, 1 },
		{ 1, -1 },
		{ 1, 0 },
		{ 1, 1 }
	};

	private static readonly int[] MoveAbsRight = new int[9] { 0, 0, 1, 0, 0, 1, 0, 0, 1 };

	private static readonly int[] MoveAbsLeft = new int[9] { 1, 0, 0, 1, 0, 0, 1, 0, 0 };

	private static readonly int[] MoveAbsCyoku = new int[9] { 0, 0, 0, 0, 0, 0, 0, 1, 0 };

	private static readonly int[] MoveActionUe = new int[9] { 0, 0, 0, 0, 0, 0, 1, 1, 1 };

	private static readonly int[] MoveActionYori = new int[9] { 0, 0, 0, 1, 0, 1, 0, 0, 0 };

	private static readonly int[] MoveActionHiki = new int[9] { 1, 1, 1, 0, 0, 0, 0, 0, 0 };

	public static bool GetMoveFromPosition(SPosition pos, MoveData moveData, MoveAbsPos move_abs_pos, MoveOperation move_action)
	{
		Piece piece = moveData.Piece;
		int num = moveData.ToSquare.FileOf();
		int num2 = moveData.ToSquare.RankOf();
		MoveCheckData[] array = new MoveCheckData[9];
		for (int i = 0; i < 9; i++)
		{
			array[i].Rank = 0;
			array[i].File = 0;
			array[i].Enable = 0;
		}
		if (pos.Turn == PlayerColor.White)
		{
			piece |= Piece.WhiteFlag;
		}
		switch (piece)
		{
		case Piece.BFU:
			CheckMove(pos, piece, num, num2 + 1, ref array[7]);
			break;
		case Piece.WFU:
			CheckMove(pos, piece, num, num2 - 1, ref array[7]);
			break;
		case Piece.BKYO:
		case Piece.WKYO:
			CheckMove(pos, piece, num, num2, 0, 1, ref array[7]);
			break;
		case Piece.BKEI:
			CheckMove(pos, piece, num - 1, num2 + 2, ref array[6]);
			CheckMove(pos, piece, num + 1, num2 + 2, ref array[8]);
			break;
		case Piece.WKEI:
			CheckMove(pos, piece, num + 1, num2 - 2, ref array[6]);
			CheckMove(pos, piece, num - 1, num2 - 2, ref array[8]);
			break;
		case Piece.BGIN:
		case Piece.WGIN:
			CheckMove(pos, piece, num, num2, GinAbsFrom, array);
			break;
		case Piece.BKAK:
		case Piece.WKAK:
			CheckMove(pos, piece, num, num2, -1, -1, ref array[0]);
			CheckMove(pos, piece, num, num2, 1, -1, ref array[2]);
			CheckMove(pos, piece, num, num2, -1, 1, ref array[6]);
			CheckMove(pos, piece, num, num2, 1, 1, ref array[8]);
			break;
		case Piece.BUMA:
		case Piece.WUMA:
			CheckMove(pos, piece, num, num2, -1, -1, ref array[0]);
			CheckMove(pos, piece, num, num2, 1, -1, ref array[2]);
			CheckMove(pos, piece, num, num2, -1, 1, ref array[6]);
			CheckMove(pos, piece, num, num2, 1, 1, ref array[8]);
			CheckMove(pos, piece, num, num2, UmaAbsFrom, array);
			break;
		case Piece.BHI:
		case Piece.WHI:
			CheckMove(pos, piece, num, num2, 0, -1, ref array[1]);
			CheckMove(pos, piece, num, num2, -1, 0, ref array[3]);
			CheckMove(pos, piece, num, num2, 1, 0, ref array[5]);
			CheckMove(pos, piece, num, num2, 0, 1, ref array[7]);
			break;
		case Piece.BRYU:
		case Piece.WRYU:
			CheckMove(pos, piece, num, num2, 0, -1, ref array[1]);
			CheckMove(pos, piece, num, num2, -1, 0, ref array[3]);
			CheckMove(pos, piece, num, num2, 1, 0, ref array[5]);
			CheckMove(pos, piece, num, num2, 0, 1, ref array[7]);
			CheckMove(pos, piece, num, num2, RyuAbsFrom, array);
			break;
		case Piece.BOU:
		case Piece.WOU:
			CheckMove(pos, piece, num, num2, GyokuAbsFrom, array);
			break;
		default:
			CheckMove(pos, piece, num, num2, KinAbsFrom, array);
			break;
		}
		switch (move_abs_pos)
		{
		case MoveAbsPos.LEFT:
			if (piece == Piece.BRYU || piece == Piece.WRYU || piece == Piece.BUMA || piece == Piece.WUMA)
			{
				MoveDataMaskLeft(array);
			}
			else
			{
				MoveDataMask(MoveAbsLeft, array);
			}
			break;
		case MoveAbsPos.RIGHT:
			if (piece == Piece.BRYU || piece == Piece.WRYU || piece == Piece.BUMA || piece == Piece.WUMA)
			{
				MoveDataMaskRight(array);
			}
			else
			{
				MoveDataMask(MoveAbsRight, array);
			}
			break;
		case MoveAbsPos.CENTER:
			MoveDataMask(MoveAbsCyoku, array);
			break;
		}
		switch (move_action)
		{
		case MoveOperation.UE:
			MoveDataMask(MoveActionUe, array);
			break;
		case MoveOperation.HIKI:
			MoveDataMask(MoveActionHiki, array);
			break;
		case MoveOperation.YORI:
			MoveDataMask(MoveActionYori, array);
			break;
		}
		int num3 = 0;
		for (int j = 0; j < 9; j++)
		{
			if (array[j].Enable == 1)
			{
				moveData.FromSquare = Square.Make(array[j].File, array[j].Rank);
				num3++;
			}
		}
		if (num3 != 1)
		{
			return false;
		}
		return true;
	}

	private static void CheckMove(SPosition pos, Piece piece, int move_to_file, int move_to_rank, int step_file, int step_rank, ref MoveCheckData move_data)
	{
		if (pos.Turn == PlayerColor.White)
		{
			step_rank = -step_rank;
			step_file = -step_file;
		}
		int num = move_to_rank + step_rank;
		for (int i = move_to_file + step_file; SPosition.InBoard(i, num); i += step_file)
		{
			Piece piece2 = pos.GetPiece(i, num);
			if (piece2 != Piece.NoPiece)
			{
				if (piece2 == piece)
				{
					move_data.File = i;
					move_data.Rank = num;
					move_data.Enable = 1;
				}
				break;
			}
			num += step_rank;
		}
	}

	private static void CheckMove(SPosition pos, Piece piece, int file, int rank, ref MoveCheckData move_data)
	{
		if (SPosition.InBoard(file, rank) && pos.GetPiece(file, rank) == piece)
		{
			move_data.Rank = rank;
			move_data.File = file;
			move_data.Enable = 1;
		}
	}

	private static void CheckMove(SPosition pos, Piece piece, int move_to_file, int move_to_rank, int[,] move_abs, MoveCheckData[] move_data)
	{
		for (int i = 0; i < 9; i++)
		{
			if (move_abs[i, 0] != 0 || move_abs[i, 1] != 0)
			{
				int rank;
				int file;
				if (pos.Turn == PlayerColor.White)
				{
					rank = move_to_rank - move_abs[i, 0];
					file = move_to_file - move_abs[i, 1];
				}
				else
				{
					rank = move_to_rank + move_abs[i, 0];
					file = move_to_file + move_abs[i, 1];
				}
				if (SPosition.InBoard(file, rank) && pos.GetPiece(file, rank) == piece)
				{
					move_data[i].Rank = rank;
					move_data[i].File = file;
					move_data[i].Enable = 1;
				}
			}
		}
	}

	private static void MoveDataMask(int[] move_mask, MoveCheckData[] move_data)
	{
		for (int i = 0; i < 9; i++)
		{
			move_data[i].Enable &= move_mask[i];
		}
	}

	private static void MoveDataMaskLeft(MoveCheckData[] move_data)
	{
		move_data[0].Enable &= 1;
		move_data[3].Enable &= 1;
		move_data[6].Enable &= 1;
		if (move_data[0].Enable == 0 && move_data[3].Enable == 0 && move_data[6].Enable == 0)
		{
			move_data[1].Enable &= 1;
			move_data[4].Enable &= 1;
			move_data[7].Enable &= 1;
		}
		else
		{
			move_data[1].Enable = 0;
			move_data[4].Enable = 0;
			move_data[7].Enable = 0;
		}
		move_data[2].Enable = 0;
		move_data[5].Enable = 0;
		move_data[8].Enable = 0;
	}

	private static void MoveDataMaskRight(MoveCheckData[] move_data)
	{
		move_data[2].Enable &= 1;
		move_data[5].Enable &= 1;
		move_data[8].Enable &= 1;
		if (move_data[2].Enable == 0 && move_data[5].Enable == 0 && move_data[8].Enable == 0)
		{
			move_data[1].Enable &= 1;
			move_data[4].Enable &= 1;
			move_data[7].Enable &= 1;
		}
		else
		{
			move_data[1].Enable = 0;
			move_data[4].Enable = 0;
			move_data[7].Enable = 0;
		}
		move_data[0].Enable = 0;
		move_data[3].Enable = 0;
		move_data[6].Enable = 0;
	}
}
