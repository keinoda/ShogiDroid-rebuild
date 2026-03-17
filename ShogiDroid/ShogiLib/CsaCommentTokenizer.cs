namespace ShogiLib;

public class CsaCommentTokenizer
{
	private int index;

	private string str;

	private string keep;

	public CsaCommentTokenizer(string str)
	{
		this.str = str;
		index = 0;
		keep = null;
	}

	public string Token()
	{
		if (keep != null)
		{
			string result = keep;
			keep = null;
			return result;
		}
		string result2 = string.Empty;
		int num = -1;
		while (index < str.Length)
		{
			if (str[index] != ' ')
			{
				if (num == -1)
				{
					num = index;
				}
			}
			else if (str[index] == ' ' && num != -1)
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

	public static bool ParseNum(string str, out int outnum)
	{
		int num = 0;
		int i = 0;
		bool flag = false;
		bool result = false;
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
				result = true;
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
		if (flag)
		{
			num = -num;
		}
		outnum = num;
		return result;
	}
}
