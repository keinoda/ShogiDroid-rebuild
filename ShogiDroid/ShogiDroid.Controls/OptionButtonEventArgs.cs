using System;

namespace ShogiDroid.Controls;

public class OptionButtonEventArgs : EventArgs
{
	public string Name { get; set; }

	public OptionButtonEventArgs(string name)
	{
		Name = name;
	}
}
