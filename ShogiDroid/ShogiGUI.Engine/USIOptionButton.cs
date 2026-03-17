using System;

namespace ShogiGUI.Engine;

[Serializable]
public class USIOptionButton : USIOption
{
	public USIOptionButton(string name)
		: base(name, USIOptionType.BUTTON)
	{
	}

	public override bool SetValue(string value)
	{
		changed_ = true;
		return true;
	}

	public override USIOption Clone()
	{
		return new USIOptionButton(name_) { changed_ = this.changed_ };
	}
}
