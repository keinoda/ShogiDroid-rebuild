using System;

namespace ShogiGUI.Events;

public class GameEventArgs : EventArgs
{
	public GameEventId EventId;

	public bool Engine;

	public GameEventArgs(GameEventId eventId)
	{
		EventId = eventId;
	}

	public GameEventArgs(GameEventId eventId, bool engine)
	{
		EventId = eventId;
		Engine = engine;
	}
}
