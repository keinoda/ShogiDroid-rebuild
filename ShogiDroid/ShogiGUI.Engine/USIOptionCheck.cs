using System;

namespace ShogiGUI.Engine;

[Serializable]
public class USIOptionCheck : USIOption
{
	private bool value_;

	public bool Value
	{
		get
		{
			return value_;
		}
		set
		{
			value_ = value;
		}
	}

	public bool DefaultValue { get; set; }

	public USIOptionCheck(string name, bool defaultValue)
		: base(name, USIOptionType.CHECK)
	{
		value_ = defaultValue;
		DefaultValue = defaultValue;
	}

	public override string ValueToString()
	{
		if (value_)
		{
			return "true";
		}
		return "false";
	}

	public override bool SetValue(bool value)
	{
		if (value_ != value)
		{
			changed_ = true;
			value_ = value;
		}
		return changed_;
	}

	public override bool SetValue(string value)
	{
		bool flag = ((value == "true" || value == "True") ? true : false);
		if (value_ != flag)
		{
			value_ = flag;
			changed_ = true;
		}
		return changed_;
	}

	public override void Reset()
	{
		changed_ = true;
		value_ = DefaultValue;
	}

	public override USIOption Clone()
	{
		return new USIOptionCheck(name_, DefaultValue) { Value = this.Value, changed_ = this.changed_ };
	}
}
