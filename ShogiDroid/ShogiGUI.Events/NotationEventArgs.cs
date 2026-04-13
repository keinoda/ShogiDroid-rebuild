using System;

namespace ShogiGUI.Events;

public class NotationEventArgs : EventArgs
{
	public NotationEventId EventId { get; set; }

	public NotationEventArgs(NotationEventId eventId)
	{
		EventId = eventId;
	}
}
