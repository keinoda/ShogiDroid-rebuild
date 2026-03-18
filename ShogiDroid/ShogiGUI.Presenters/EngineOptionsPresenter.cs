using ShogiGUI.Engine;
using ShogiGUI.Events;

namespace ShogiGUI.Presenters;

public class EngineOptionsPresenter : PresenterBase<IEngineOptions>
{
	public EnginePlayer EnginePlayer => Domain.Game.EnginePlayer;

	public EngineOptionsPresenter(IEngineOptions view)
		: base(view)
	{
	}

	public override void Initialize()
	{
		Domain.Game.GameEventHandler += Game_GameEventHandler;
		Domain.Game.EngineWakeup();
	}

	public override void Resume()
	{
	}

	public override void Pause()
	{
	}

	public override void Destory()
	{
		Domain.Game.GameEventHandler -= Game_GameEventHandler;
	}

	public void SendButton(string name)
	{
		if (Domain.Game.EnginePlayer != null)
		{
			Domain.Game.EnginePlayer.SetOption(name, string.Empty, temp: true);
		}
	}

	private void Game_GameEventHandler(object sender, GameEventArgs e)
	{
		if (e.EventId == GameEventId.InitializeEnd)
		{
			view.InitializeEnd();
		}
		else if (e.EventId == GameEventId.InitializeError)
		{
			view.InitializeError();
		}
	}
}
