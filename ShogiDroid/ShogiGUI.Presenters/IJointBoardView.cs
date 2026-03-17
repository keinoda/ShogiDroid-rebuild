using ShogiGUI.Events;

namespace ShogiGUI.Presenters;

public interface IJointBoardView
{
	void UpdateNotation(NotationEventId id);
}
