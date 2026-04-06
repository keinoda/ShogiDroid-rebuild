using ShogiLib;

namespace ShogiGUI.Presenters;

public class EditBoardPresenter : PresenterBase<IEditBoardView>
{
	private SNotation notation;

	public SNotation Notation => notation;

	public EditBoardPresenter(IEditBoardView view)
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

	public override void Destroy()
	{
	}

	public void BoardEditEnd()
	{
		Domain.Game.NotationModel.EditBoard(notation);
	}

	public void InitPositionEven()
	{
		notation.Position.Init();
	}

	public void InitPositionMate()
	{
		notation.Position.InitMatePosition();
	}

	public void Mirror()
	{
		notation.Position.Reverse();
		string blackName = notation.BlackName;
		notation.BlackName = notation.WhiteName;
		notation.WhiteName = blackName;
	}

	public void ChangeTurn()
	{
		notation.Position.Turn = notation.Position.Turn.Opp();
	}
}
