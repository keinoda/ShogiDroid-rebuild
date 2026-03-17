using ShogiLib;

namespace ShogiGUI.Engine;

public interface IPlayer
{
	bool Init(string filename);

	void Terminate();

	void Ready();

	void GameStart();

	void GameOver(PlayerColor wincolor);

	int Go(SNotation notation, GameTimer time_info);

	int Ponder(SNotation notationm, GameTimer time_info);

	void MoveNow();

	void Stop();
}
