namespace ShogiGUI.Presenters;

public abstract class PresenterBase<T>
{
	protected T view;

	public PresenterBase(T view)
	{
		this.view = view;
	}

	public abstract void Initialize();

	public abstract void Resume();

	public abstract void Pause();

	public abstract void Destory();
}
