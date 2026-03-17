using System;

namespace ShogiGUI.Engine;

[Serializable]
public class USIOptionSpin : USIOption
{
	public int Value { get; set; }

	public int DefaultValue { get; set; }

	public int Min { get; set; }

	public int Max { get; set; }

	public USIOptionSpin(string name, int defaultValue, int min, int max)
		: base(name, USIOptionType.SPIN)
	{
		Value = defaultValue;
		DefaultValue = defaultValue;
		Min = min;
		Max = max;
	}

	public override string ValueToString()
	{
		return Value.ToString();
	}

	public override bool SetValue(int value)
	{
		if (Value != value)
		{
			changed_ = true;
			Value = value;
		}
		return changed_;
	}

	public override bool SetValue(string value)
	{
		USIString.ParseNum(value, out int out_num);
		if (Value != out_num)
		{
			Value = out_num;
			changed_ = true;
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
		return new USIOptionSpin(name_, DefaultValue, Min, Max) { Value = this.Value, changed_ = this.changed_ };
	}
}
