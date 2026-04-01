using ShogiLib;

namespace ShogiGUI;

public class AppSettings
{
	public int BlackNo;

	public int WhiteNo = 1;

	public CPULevel CpuLevel;

	public Handicap Handicap;

	public GameStartPosition StartPosition;

	public GameStartMode StartMode;

	public bool AutoSave = true;

	public AnimeSpeed AnimationSpeed = AnimeSpeed.Normal;

	public bool ShowHintArrow = true;

	public bool ShowNextArrow = true;

	public bool ShowComputerThinking;

	public MoveStyle MoveStyle;

	public bool PlaySE = true;

	public string PlayerName = "Player";

	public string FileName = string.Empty;

	public float EvalGraphScaleFactor = 1f;

	public int PlayInterval = 500;

	public bool Reverse;

	public int CustomMenuButton;

	public int ReverseButotn = 50;

	public int ShortcutMenu1 = 22;

	public int ShortcutMenu2 = 23;

	public int ShortcutMenu3;

	public int ShortcutMenu4;

	public int ShortcutMenu5;

	public int ShortcutMenu6 = 25;

	public int NotationAnalyzeCount;

	public bool DispToolbar;

	public int PVDisplay;

	public bool GraphLiner = true;

	public string ThemeMode = "system";

	public string WarsUserName = string.Empty;

	public string ImportUrl = string.Empty;

	public bool ConvertEvalToWinRate = false;

	public string WinRateCoefficient = "750";

	public bool AutoThreatmateAnalysis = true;

	public bool HideInternalEngine = false;

	public AppSettings()
	{
	}
}
