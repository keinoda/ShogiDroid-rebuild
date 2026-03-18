using ShogiGUI.Engine;
using ShogiGUI.Events;

namespace ShogiGUI.Presenters;

public interface IMainView
{
	void UpdateNotation(NotationEventId eventid);

	void UpdateState();

	void UpdateInfo(PvInfos pvinfos);

	void SetPlayer(bool black, bool white);

	void MessageError(string error);

	void Message(MainViewMessageId id);

	void Moved(bool engine);

	void UpdateReverse();

	void UpdateTime();

	void AutoPlayState(bool play);

	void ShowInterstitial();
}
