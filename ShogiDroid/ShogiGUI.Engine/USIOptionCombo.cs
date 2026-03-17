using System;
using System.Collections.Generic;

namespace ShogiGUI.Engine;

[Serializable]
public class USIOptionCombo : USIOption
{
	private List<string> comboValues_;

	public string Value { get; set; }

	public string DefaultValue { get; set; }

	public List<string> ComboValues
	{
		get
		{
			return comboValues_;
		}
		set
		{
			comboValues_ = value;
		}
	}

	public USIOptionCombo(string name, string defaultValue, List<string> combo)
		: base(name, USIOptionType.COMBO)
	{
		Value = defaultValue;
		DefaultValue = defaultValue;
		comboValues_ = combo;
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

	public override bool SetValue(int value)
	{
		if (value >= 0 && value < ComboValues.Count)
		{
			changed_ = true;
			Value = ComboValues[value];
		}
		return changed_;
	}

	public override void Reset()
	{
		changed_ = true;
		Value = DefaultValue;
	}

	public override USIOption Clone()
	{
		return new USIOptionCombo(name_, DefaultValue, new List<string>(comboValues_)) { Value = this.Value, changed_ = this.changed_ };
	}
}
