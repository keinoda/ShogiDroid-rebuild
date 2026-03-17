using ShogiGUI.Engine;
using ShogiGUI.Events;
using ShogiGUI.Models;
using ShogiLib;

namespace ShogiGUI.Presenters;

public class JointBoardPresenter : PresenterBase<IJointBoardView>
{
	private SNotation notation;

	public SNotation Notation => notation;

	public JointBoardPresenter(IJointBoardView view)
		: base(view)
	{
		notation = new SNotation(Domain.Game.Notation);
		notation.InitEdit();
	}

	public override void Initialize()
	{
	}

	public override void Resume()
	{
	}

	public override void Pause()
	{
	}

	public override void Destory()
	{
	}

	public void LoadPv(int pvnum, PVDispMode dispMode)
	{
		PvInfo pvInfo = Domain.Game.PvInfos.GetPvInfo(pvnum, dispMode);
		if (pvInfo != null)
		{
			NotationModel.SetMoves(notation, Domain.Game.Notation.Position, null, pvInfo.PvMoves);
		}
	}

	public void Next()
	{
		notation.Next(1);
		view.UpdateNotation(NotationEventId.NEXT);
	}

	public void Prev()
	{
		notation.Prev(1);
		view.UpdateNotation(NotationEventId.PREV);
	}
}
