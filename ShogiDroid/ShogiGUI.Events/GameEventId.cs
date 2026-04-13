namespace ShogiGUI.Events;

public enum GameEventId
{
	GameStart,
	Moved,
	Delay,
	GameOver,
	TakeTurn,
	GameEnd,
	Info,
	AnalyzeStart,
	AnalyzeEnd,
	NotationAnalyzeEnd,
	MateStart,
	MateEnd,
	InitializeStart,
	InitializeEnd,
	InitializeError,
	ThreatmateUpdated,
	PolicyUpdated,
	UpdateTime,
}
