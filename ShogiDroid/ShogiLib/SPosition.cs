using System;
using System.Collections.Generic;

namespace ShogiLib;

public class SPosition : ICloneable
{
	public const int HandMax = 9;

	private PlayerColor turn;

	private int[][] hand;

	private int[] blackHand;

	private int[] whiteHand;

	private Piece[] board;

	private MoveData moveLast;

	private HashKey hashkey;

	public HashKey HashKey => hashkey;

	public PlayerColor Turn
	{
		get
		{
			return turn;
		}
		set
		{
			if (turn != value)
			{
				turn = value;
				hashkey.Init(this);
			}
		}
	}

	public IEnumerable<Piece> Board
	{
		get
		{
			Piece[] array = board;
			for (int i = 0; i < array.Length; i++)
			{
				yield return array[i];
			}
		}
	}

	public int[] BlackHand => blackHand;

	public int[] WhiteHand => whiteHand;

	public MoveData MoveLast => moveLast;

	public SPosition()
	{
		blackHand = new int[9];
		whiteHand = new int[9];
		hand = new int[2][] { blackHand, whiteHand };
		board = new Piece[81];
		moveLast = new MoveData();
		hashkey = default(HashKey);
		Init();
	}

	public void Init()
	{
		ResetBoard();
		hashkey.Init(this);
	}

	private void ResetBoard()
	{
		turn = PlayerColor.Black;
		for (int i = 0; i < 9; i++)
		{
			blackHand[i] = 0;
			whiteHand[i] = 0;
		}
		for (int j = 0; j < 81; j++)
		{
			board[j] = Piece.NoPiece;
		}
		board[0] = Piece.WKYO;
		board[1] = Piece.WKEI;
		board[2] = Piece.WGIN;
		board[3] = Piece.WKIN;
		board[4] = Piece.WOU;
		board[5] = Piece.WKIN;
		board[6] = Piece.WGIN;
		board[7] = Piece.WKEI;
		board[8] = Piece.WKYO;
		board[10] = Piece.WHI;
		board[16] = Piece.WKAK;
		for (int k = 18; k <= 26; k++)
		{
			board[k] = Piece.WFU;
		}
		for (int l = 54; l <= 62; l++)
		{
			board[l] = Piece.BFU;
		}
		board[70] = Piece.BHI;
		board[64] = Piece.BKAK;
		board[72] = Piece.BKYO;
		board[73] = Piece.BKEI;
		board[74] = Piece.BGIN;
		board[75] = Piece.BKIN;
		board[76] = Piece.BOU;
		board[77] = Piece.BKIN;
		board[78] = Piece.BGIN;
		board[79] = Piece.BKEI;
		board[80] = Piece.BKYO;
		moveLast.Initialize();
	}

	public bool Move(MoveData moveData)
	{
		bool flag = true;
		if (moveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			flag = MoveDrop(moveData);
		}
		else if (moveData.MoveType != MoveType.Pass)
		{
			flag = MoveNormal(moveData);
		}
		if (!flag)
		{
			return false;
		}
		hashkey.MoveHash(moveData);
		MoveData.Copy(moveLast, moveData);
		turn = turn.Opp();
		return flag;
	}

	private bool MoveNormal(MoveData moveData)
	{
		Piece piece = moveData.Piece | PieceExtensions.PieceFlagFromColor(turn);
		if (moveData.MoveType.HasFlag(MoveType.MoveMask))
		{
			piece |= Piece.BOU;
		}
		board[moveData.FromSquare] = Piece.NoPiece;
		board[moveData.ToSquare] = piece;
		if (moveData.CapturePiece != Piece.NoPiece)
		{
			if (turn == PlayerColor.White)
			{
				whiteHand[moveData.CapturePiece.ToHandIndex()]++;
			}
			else
			{
				blackHand[moveData.CapturePiece.ToHandIndex()]++;
			}
		}
		return true;
	}

	private bool MoveDrop(MoveData moveData)
	{
		Piece piece = moveData.Piece | PieceExtensions.PieceFlagFromColor(turn);
		if (!IsHand(turn, moveData.Piece.ToHandIndex()))
		{
			return false;
		}
		hand[(int)turn][moveData.Piece.ToHandIndex()]--;
		board[moveData.ToSquare] = piece;
		return true;
	}

	public bool UnMove(MoveData moveData, MoveData curent)
	{
		bool flag = true;
		if (moveData.MoveType.HasFlag(MoveType.DropFlag))
		{
			flag = UnMoveDrop(moveData);
		}
		else if (moveData.MoveType != MoveType.Pass)
		{
			flag = UnMoveNormal(moveData);
		}
		if (!flag)
		{
			return false;
		}
		if (curent != null)
		{
			MoveData.Copy(moveLast, curent);
		}
		hashkey.UnMoveHash(moveData);
		turn = turn.Opp();
		return flag;
	}

	private bool UnMoveNormal(MoveData moveData)
	{
		Piece piece = moveData.Piece;
		if (moveData.MoveType.HasFlag(MoveType.MoveMask))
		{
			piece &= (Piece)247;
		}
		board[moveData.FromSquare] = piece;
		board[moveData.ToSquare] = moveData.CapturePiece;
		if (moveData.CapturePiece != Piece.NoPiece)
		{
			if (turn.Opp() == PlayerColor.White)
			{
				whiteHand[moveData.CapturePiece.ToHandIndex()]--;
			}
			else
			{
				blackHand[moveData.CapturePiece.ToHandIndex()]--;
			}
		}
		return true;
	}

	private bool UnMoveDrop(MoveData moveData)
	{
		PlayerColor playerColor = Turn.Opp();
		_ = moveData.Piece;
		hand[(int)playerColor][moveData.Piece.ToHandIndex()]++;
		board[moveData.ToSquare] = Piece.NoPiece;
		return true;
	}

	public object Clone()
	{
		SPosition sPosition = (SPosition)MemberwiseClone();
		if (blackHand != null)
		{
			sPosition.blackHand = (int[])blackHand.Clone();
		}
		if (whiteHand != null)
		{
			sPosition.whiteHand = (int[])whiteHand.Clone();
		}
		sPosition.hand = new int[2][] { sPosition.blackHand, sPosition.whiteHand };
		if (board != null)
		{
			sPosition.board = (Piece[])board.Clone();
		}
		if (moveLast != null)
		{
			sPosition.moveLast = new MoveData(moveLast);
		}
		sPosition.hashkey = new HashKey(hashkey);
		return sPosition;
	}

	public int SearchPiece(Piece piece)
	{
		int i;
		for (i = 0; i < 81 && board[i] != piece; i++)
		{
		}
		return i;
	}

	public bool IsBlack(int file, int rank)
	{
		return board[rank * 9 + file].ColorOf() == PlayerColor.Black;
	}

	public bool IsBlack(File file, Rank rank)
	{
		return IsBlack((int)file, (int)rank);
	}

	public bool IsWhite(int file, int rank)
	{
		return board[rank * 9 + file].ColorOf() == PlayerColor.White;
	}

	public bool IsWhite(File file, Rank rank)
	{
		return IsWhite((int)file, (int)rank);
	}

	public bool IsEmpty(int file, int rank)
	{
		return board[rank * 9 + file] == Piece.NoPiece;
	}

	public bool IsEmpty(File file, Rank rank)
	{
		return IsEmpty((int)file, (int)rank);
	}

	public bool IsHand(PlayerColor color, int pieceType)
	{
		return IsHand(color, (PieceType)pieceType);
	}

	public bool IsHand(PlayerColor color, PieceType pieceType)
	{
		if ((int)pieceType < 1 || (int)pieceType > 7)
		{
			return false;
		}
		if (hand[(int)color][(uint)pieceType] == 0)
		{
			return false;
		}
		return true;
	}

	public int GetHand(PlayerColor color, PieceType pieceType)
	{
		return hand[(int)color][(uint)pieceType];
	}

	public int GetHand(PlayerColor color, int pieceType)
	{
		return hand[(int)color][pieceType];
	}

	public int GetBlackHand(int pieceType)
	{
		return blackHand[pieceType];
	}

	public int GetBlackHand(PieceType pieceType)
	{
		return blackHand[(uint)pieceType];
	}

	public void SetBlackHand(int pieceType, int num)
	{
		blackHand[pieceType] = num;
	}

	public void SetBlackHand(PieceType pieceType, int num)
	{
		blackHand[(uint)pieceType] = num;
	}

	public int GetWhiteHand(int piece)
	{
		return whiteHand[piece];
	}

	public int GetWhiteHand(PieceType piece)
	{
		return whiteHand[(uint)piece];
	}

	public void SetWhiteHand(int piece, int num)
	{
		whiteHand[piece] = num;
	}

	public void SetWhiteHand(PieceType piece, int num)
	{
		whiteHand[(uint)piece] = num;
	}

	public Piece GetPiece(int sq)
	{
		return board[sq];
	}

	public Piece GetPiece(int file, int rank)
	{
		return board[rank * 9 + file];
	}

	public void SetPiece(int file, int rank, Piece piece)
	{
		board[rank * 9 + file] = piece;
	}

	public void SetPiece(int sq, Piece piece)
	{
		board[sq] = piece;
	}

	public void SetHandicapKyo()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[8] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicapRightKyo()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicapKaku()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicapHisya()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[10] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicapHiKyo()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[8] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicap2()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicap3()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicap4()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		board[8] = Piece.NoPiece;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicap5()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		board[8] = Piece.NoPiece;
		board[1] = Piece.NoPiece;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicapLeft5()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		board[8] = Piece.NoPiece;
		board[7] = Piece.NoPiece;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicap6()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		board[8] = Piece.NoPiece;
		board[7] = Piece.NoPiece;
		board[1] = Piece.NoPiece;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicap8()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		board[8] = Piece.NoPiece;
		board[7] = Piece.NoPiece;
		board[6] = Piece.NoPiece;
		board[2] = Piece.NoPiece;
		board[1] = Piece.NoPiece;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void SetHandicap10()
	{
		ResetBoard();
		turn = PlayerColor.White;
		board[16] = Piece.NoPiece;
		board[10] = Piece.NoPiece;
		board[8] = Piece.NoPiece;
		board[7] = Piece.NoPiece;
		board[6] = Piece.NoPiece;
		board[5] = Piece.NoPiece;
		board[3] = Piece.NoPiece;
		board[2] = Piece.NoPiece;
		board[1] = Piece.NoPiece;
		board[0] = Piece.NoPiece;
		hashkey.Init(this);
	}

	public void Reverse()
	{
		for (int i = 0; i < 9; i++)
		{
			int num = blackHand[i];
			blackHand[i] = whiteHand[i];
			whiteHand[i] = num;
		}
		int num2 = 0;
		int num3 = 80;
		while (num2 <= 40)
		{
			Piece piece = board[num2];
			board[num2] = board[num3].Opp();
			board[num3] = piece.Opp();
			num2++;
			num3--;
		}
		hashkey.Init(this);
	}

	public void InitMatePosition()
	{
		turn = PlayerColor.Black;
		for (int i = 0; i < 9; i++)
		{
			blackHand[i] = 0;
			whiteHand[i] = 0;
		}
		for (int j = 0; j < 81; j++)
		{
			board[j] = Piece.NoPiece;
		}
		board[4] = Piece.WOU;
		WhiteHand[1] = 18;
		WhiteHand[2] = 4;
		WhiteHand[3] = 4;
		WhiteHand[4] = 4;
		WhiteHand[5] = 4;
		WhiteHand[6] = 2;
		WhiteHand[7] = 2;
		hashkey.Init(this);
	}

	public void AllPieceToBox()
	{
		turn = PlayerColor.Black;
		for (int i = 0; i < 9; i++)
		{
			blackHand[i] = 0;
			whiteHand[i] = 0;
		}
		for (int j = 0; j < 81; j++)
		{
			board[j] = Piece.NoPiece;
		}
		hashkey.Init(this);
	}

	public void InitHashKey()
	{
		hashkey.Init(this);
	}

	public void BoardClear()
	{
		for (int i = 0; i < 81; i++)
		{
			board[i] = Piece.NoPiece;
		}
		for (int j = 0; j < 9; j++)
		{
			blackHand[j] = 0;
			whiteHand[j] = 0;
		}
	}

	public static bool InBoard(int file, int rank)
	{
		if (file >= 0 && file < 9 && rank >= 0 && rank < 9)
		{
			return true;
		}
		return false;
	}

	public bool Equals(SPosition pos)
	{
		for (int i = 0; i < 81; i++)
		{
			if (board[i] != pos.board[i])
			{
				return false;
			}
		}
		for (int j = 0; j < blackHand.Length; j++)
		{
			if (blackHand[j] != pos.blackHand[j])
			{
				return false;
			}
		}
		for (int k = 0; k < whiteHand.Length; k++)
		{
			if (whiteHand[k] != pos.whiteHand[k])
			{
				return false;
			}
		}
		if (turn != pos.turn)
		{
			return false;
		}
		return true;
	}
}
