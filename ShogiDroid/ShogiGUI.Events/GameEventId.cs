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
	UpdateTime,
	/// <summary>
	/// リモートエンジン接続失敗時、vast.aiインスタンスの起動が必要。
	/// UI側で自動起動処理を行い、完了後に ResumeAfterVastAiBoot() を呼ぶ。
	/// </summary>
	VastAiBootRequired
}
