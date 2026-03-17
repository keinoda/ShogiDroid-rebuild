using System;

namespace ShogiGUI.Engine;

[Serializable]
public class USIOptionString : USIOption
{
	public string Value { get; set; }

	public string DefaultValue { get; set; }

	public USIOptionString(string name, string defaultValue)
		: base(name, USIOptionType.STRING)
	{
		Value = defaultValue;
		DefaultValue = defaultValue;
	}

	public override string ValueToString()
	{
		return Value;
	}

	public override bool SetValue(string value)
	{
		changed_ = true;
		Value = value;
		return true;
	}

	public override void Reset()
	{
		changed_ = true;
		Value = DefaultValue;
	}

	public override USIOption Clone()
	{
		return new USIOptionString(name_, DefaultValue) { Value = this.Value, changed_ = this.changed_ };
	}
}
