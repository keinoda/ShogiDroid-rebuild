using System;
using System.Collections.Generic;
using ShogiGUI.Engine;
using ShogiLib;

namespace ShogiGUI.Models;

public class HumanPlayer : IPlayer
{
	private PlayerColor color;

	public event EventHandler<InitializedEventArgs> Initialized;

	public event EventHandler<ReadyOkEventArgs> ReadyOk;

	public HumanPlayer(PlayerColor color)
	{
		this.color = color;
	}

	public bool Init(string filename)
	{
		OnInitialized(new InitializedEventArgs(color));
		return true;
	}

	public void Terminate()
	{
	}

	public void Ready()
	{
		OnReadyOk(new ReadyOkEventArgs(color));
	}

	public void GameStart()
	{
	}

	public void GameOver(PlayerColor color)
	{
	}

	public int Go(SNotation notation, GameTimer time_info)
	{
		return 0;
	}

	public int Ponder(SNotation notation, GameTimer time_info)
	{
		return 0;
	}

	public void MoveNow()
	{
	}

	public void Stop()
	{
	}

	public void SetOptions(Dictionary<string, string> opt_name_value)
	{
	}

	protected virtual void OnInitialized(InitializedEventArgs e)
	{
		if (this.Initialized != null)
		{
			this.Initialized(this, e);
		}
	}

	protected virtual void OnReadyOk(ReadyOkEventArgs e)
	{
		if (this.ReadyOk != null)
		{
			this.ReadyOk(this, e);
		}
	}
}
