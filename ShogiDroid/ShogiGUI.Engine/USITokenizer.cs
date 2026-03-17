namespace ShogiGUI.Engine;

public class USITokenizer
{
	private string input_string_;

	private int pos_;

	public USITokenizer(string str)
	{
		input_string_ = str;
	}

	public string GetToken()
	{
		int i;
		for (i = pos_; i < input_string_.Length && input_string_[i] == ' '; i++)
		{
		}
		int num = i;
		for (; i < input_string_.Length && input_string_[i] != ' '; i++)
		{
		}
		pos_ = i + 1;
		int num2 = i - num;
		if (num2 <= 0)
		{
			return string.Empty;
		}
		return input_string_.Substring(num, num2);
	}

	public string GetToken(string next)
	{
		int num = pos_;
		int num2 = num;
		num = input_string_.IndexOf(" " + next + " ", num);
		if (num < 0)
		{
			num = input_string_.Length;
		}
		pos_ = num + 1;
		int num3 = num - num2;
		if (num3 <= 0)
		{
			return string.Empty;
		}
		return input_string_.Substring(num2, num3);
	}

	public string GetTokenName(string next)
	{
		int i;
		for (i = pos_; i < input_string_.Length && input_string_[i] == ' '; i++)
		{
		}
		int num = i;
		i = input_string_.IndexOf(" " + next + " ", i);
		if (i < 0)
		{
			i = input_string_.Length;
		}
		pos_ = i + 1;
		int num2 = i - num;
		if (num2 <= 0)
		{
			return string.Empty;
		}
		return input_string_.Substring(num, num2);
	}

	public string GetTokenLast()
	{
		int i;
		for (i = pos_; i < input_string_.Length && input_string_[i] == ' '; i++)
		{
		}
		pos_ = input_string_.Length;
		int num = input_string_.Length - i;
		if (num <= 0)
		{
			return string.Empty;
		}
		return input_string_.Substring(i, num);
	}

	public void Back(string token)
	{
		pos_ -= token.Length;
	}
}
