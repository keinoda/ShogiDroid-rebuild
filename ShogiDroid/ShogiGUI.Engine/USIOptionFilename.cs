using System;

namespace ShogiGUI.Engine;

[Serializable]
public class USIOptionFilename : USIOptionString
{
	public USIOptionFilename(string name, string defaultValue)
		: base(name, defaultValue)
	{
		type_ = USIOptionType.FILENAME;
	}

	public override USIOption Clone()
	{
		return new USIOptionFilename(name_, DefaultValue) { Value = this.Value, changed_ = this.changed_ };
	}
}
