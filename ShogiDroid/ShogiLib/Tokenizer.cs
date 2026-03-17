namespace ShogiLib;

internal class Tokenizer
{
	private int index;

	private string str;

	private string temp;

	public Tokenizer(string str)
	{
		this.str = str;
		index = 0;
		temp = null;
	}

	public bool IsSeparator(char ch)
	{
		if (ch != ' ' && ch != '\n')
		{
			return ch == '\r';
		}
		return true;
	}

	public string Token()
	{
		if (temp != null)
		{
			string result = temp;
			temp = null;
			return result;
		}
		string result2 = string.Empty;
		int num = -1;
		while (index < str.Length)
		{
			if (!IsSeparator(str[index]))
			{
				if (num == -1)
				{
					num = index;
				}
			}
			else if (IsSeparator(str[index]) && num != -1)
			{
				break;
			}
			index++;
		}
		if (num != -1)
		{
			result2 = str.Substring(num, index - num);
		}
		return result2;
	}

	public string TokenName()
	{
		while (index < str.Length && IsSeparator(str[index]))
		{
			index++;
		}
		string result;
		if (index >= str.Length)
		{
			result = string.Empty;
		}
		else
		{
			int num = str.IndexOf(" type", index);
			if (num <= index)
			{
				result = string.Empty;
			}
			else
			{
				result = str.Substring(index, num - index);
				index = num + 1;
			}
		}
		return result;
	}

	public string TokenPosition()
	{
		while (index < str.Length && IsSeparator(str[index]))
		{
			index++;
		}
		string result;
		if (index >= str.Length)
		{
			result = string.Empty;
		}
		else
		{
			int num = str.IndexOf("moves", index);
			if (num <= index)
			{
				result = str.Substring(index, str.Length - index);
				index = str.Length;
			}
			else
			{
				result = str.Substring(index, num - index);
				index = num;
			}
		}
		return result;
	}

	public string Last()
	{
		string result = string.Empty;
		if (index + 1 < str.Length)
		{
			result = str.Substring(index + 1, str.Length - index - 1);
		}
		return result;
	}

	public static long ParseNum(string str, out int cnt)
	{
		long num = 0L;
		int i = 0;
		bool flag = false;
		if (str.Length >= 1 && str[0] == '-')
		{
			flag = true;
			i++;
		}
		for (; i < str.Length; i++)
		{
			char c = str[i];
			if (c >= '0' && c <= '9')
			{
				num *= 10;
				num += c - 48;
				continue;
			}
			switch (c)
			{
			case 'K':
			case 'k':
				num *= 1000;
				break;
			case 'M':
			case 'm':
				num = num * 1000 * 1000;
				break;
			}
			break;
		}
		cnt = i;
		if (flag)
		{
			num = -num;
		}
		return num;
	}

	public static long ParseNum(string str)
	{
		int cnt;
		return ParseNum(str, out cnt);
	}

	public void Push(string str)
	{
		temp = str;
	}
}
