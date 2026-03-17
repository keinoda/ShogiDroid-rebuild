namespace ShogiLib;

public enum MoveType : byte
{
	NoMove = 0,
	MoveFlag = 32,
	DropFlag = 64,
	ResultFlag = 128,
	MoveMask = 33,
	Normal = 32,
	Promotion = 33,
	Unpromotion = 34,
	Capture = 36,
	Same = 40,
	Pass = 48,
	Drop = 64,
	Resign = 128,
	Stop = 129,
	Repetition = 130,
	Draw = 131,
	Timeout = 132,
	Mate = 133,
	NonMate = 134,
	LoseFoul = 135,
	WinFoul = 136,
	WinNyugyoku = 137,
	LoseNyugyoku = 138,
	RepeSup = 139,
	RepeInf = 140
}
