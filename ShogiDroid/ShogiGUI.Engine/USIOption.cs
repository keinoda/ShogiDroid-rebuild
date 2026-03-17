using System;

namespace ShogiGUI.Engine;

[Serializable]
public class USIOption
{
	protected string name_;

	protected USIOptionType type_;

	protected bool changed_;

	public string Name
	{
		get
		{
			return name_;
		}
		set
		{
			name_ = value;
		}
	}

	public bool Changed
	{
		get
		{
			return changed_;
		}
		set
		{
			changed_ = value;
		}
	}

	public USIOptionType Type
	{
		get
		{
			return type_;
		}
		set
		{
			type_ = value;
		}
	}

	public USIOption(string name, USIOptionType type)
	{
		name_ = name;
		type_ = type;
	}

	public virtual string ValueToString()
	{
		return string.Empty;
	}

	public virtual bool SetValue(bool value)
	{
		return false;
	}

	public virtual bool SetValue(int value)
	{
		return false;
	}

	public virtual bool SetValue(string value)
	{
		return false;
	}

	public virtual void Reset()
	{
	}

	public bool HasChanged()
	{
		return changed_;
	}

	public void ClearChanged()
	{
		changed_ = false;
	}

	public virtual USIOption Clone()
	{
		return new USIOption(name_, type_) { changed_ = this.changed_ };
	}
}
