namespace ShogiGUI.Engine;

public class USIString
{
	public static bool ParseNum(string str, out int out_num)
	{
		int i = 0;
		bool flag = false;
		bool result = false;
		int num = 0;
		out_num = 0;
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
		out_num = num;
		return result;
	}

	public static bool ParseNum(string str, out long out_num)
	{
		int i = 0;
		bool flag = false;
		bool result = false;
		long num = 0L;
		out_num = 0L;
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
		out_num = num;
		return result;
	}
}
