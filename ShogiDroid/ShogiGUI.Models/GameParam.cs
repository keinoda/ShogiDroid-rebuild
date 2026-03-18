using ShogiLib;

namespace ShogiGUI.Models;

public class GameParam
{
	public int BlackNo;

	public int WhiteNo;

	public string BlackName;

	public string WhiteName;

	public int Time = 300000;

	public int Countdown = 10000;

	public int Increment = 0;

	public GameStartPosition StartPosition;

	public GameStartMode StartMode;

	public Handicap Handicap;

	public GameParam()
	{
	}

	public GameParam(GameParam param)
	{
		BlackNo = param.BlackNo;
		WhiteNo = param.WhiteNo;
		BlackName = param.BlackName;
		WhiteName = param.WhiteName;
		Time = param.Time;
		Countdown = param.Countdown;
		Increment = param.Increment;
		StartPosition = param.StartPosition;
		StartMode = param.StartMode;
		Handicap = param.Handicap;
	}
}
