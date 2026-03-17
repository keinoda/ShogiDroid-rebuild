using System;

namespace ShogiLib;

public class AnalyzeCommentTokenizer
{
	private int index;

	private string str;

	private string keep;

	public AnalyzeCommentTokenizer(string str)
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

	public string Last()
	{
		string result = string.Empty;
		if (index + 1 < str.Length)
		{
			result = str.Substring(index + 1, str.Length - index - 1);
		}
		return result;
	}

	public void Push(string str)
	{
		keep = str;
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

	public static long ParseTime(string str)
	{
		long num = 0L;
		string[] array = str.Split(new char[3] { ':', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length >= 2)
		{
			num = ParseNum(array[0]) * 60 * 1000;
			num += ParseNum(array[1]) * 1000;
		}
		if (array.Length >= 3)
		{
			num += ParseNum(array[2]) * 100;
		}
		return num;
	}
}
