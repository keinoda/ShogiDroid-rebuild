using System;

namespace ShogiLib;

public class NotationException : Exception
{
	public string Error;

	public int Line;

	public NotationException(string msg, string err, int line)
		: base(msg, null)
	{
		Error = err;
		Line = line;
	}
}
