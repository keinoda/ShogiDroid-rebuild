using System;

namespace ShogiGUI.Events;

public class NotationEventArgs : EventArgs
{
	public NotationEventId EventId;

	public NotationEventArgs(NotationEventId eventId)
	{
		EventId = eventId;
	}
}
