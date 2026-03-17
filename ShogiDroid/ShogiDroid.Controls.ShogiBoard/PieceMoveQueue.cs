using System.Collections;

namespace ShogiDroid.Controls.ShogiBoard;

public class PieceMoveQueue
{
	private Queue queue;

	public PieceMoveQueue()
	{
		queue = new Queue();
	}

	public void Add(PieceMoveData obj)
	{
		queue.Enqueue(obj);
	}

	public PieceMoveData Get()
	{
		PieceMoveData result = null;
		if (queue.Count != 0)
		{
			result = (PieceMoveData)queue.Dequeue();
		}
		return result;
	}

	public void Clear()
	{
		queue.Clear();
	}

	public bool IsEmpty()
	{
		return queue.Count == 0;
	}
}
