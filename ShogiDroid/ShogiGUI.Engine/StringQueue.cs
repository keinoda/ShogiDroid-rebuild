using System;
using System.Collections.Generic;
using System.Threading;

namespace ShogiGUI.Engine;

public class StringQueue : IDisposable
{
	public enum Error
	{
		OK,
		ERR,
		TIMEOUT,
		CLOSE
	}

	private Queue<string> queue_ = new Queue<string>();

	private SemaphoreSlim sem_ = new SemaphoreSlim(0);

	private bool close_;

	public void Push(string str)
	{
		lock (queue_)
		{
			if (!close_)
			{
				queue_.Enqueue(str);
				sem_.Release();
			}
		}
	}

	public Error Pop(out string str, int timeout)
	{
		str = string.Empty;
		if (close_)
		{
			return Error.CLOSE;
		}
		if (!sem_.Wait(timeout))
		{
			return Error.TIMEOUT;
		}
		lock (queue_)
		{
			if (close_)
			{
				return Error.CLOSE;
			}
			if (queue_.Count == 0)
			{
				return Error.ERR;
			}
			str = queue_.Dequeue();
			if (str == null)
			{
				return Error.ERR;
			}
		}
		return Error.OK;
	}

	public void Close()
	{
		lock (queue_)
		{
			close_ = true;
			sem_.Release();
		}
	}

	public bool IsClose()
	{
		return close_;
	}

	public void Dispose()
	{
		if (sem_ != null)
		{
			sem_.Dispose();
		}
	}
}
