using System.Collections.Generic;

namespace ShogiGUI.Engine;

public class USIOptions : Dictionary<string, USIOption>
{
	public USIOptions()
	{
		Add("USI_Hash", new USIOptionSpin("USI_Hash", 64, 1, 4096)
		{
			Changed = true
		});
		Add("USI_Ponder", new USIOptionCheck("USI_Ponder", defaultValue: false)
		{
			Changed = true
		});
	}

	public USIOption GetOption(string name)
	{
		if (ContainsKey(name))
		{
			return base[name];
		}
		return null;
	}

	public bool AddOption(string str)
	{
		USITokenizer uSITokenizer = new USITokenizer(str);
		if (uSITokenizer.GetToken() != "option")
		{
			return false;
		}
		if (uSITokenizer.GetToken() != "name")
		{
			return false;
		}
		string tokenName = uSITokenizer.GetTokenName("type");
		if (tokenName == string.Empty)
		{
			return false;
		}
		if (uSITokenizer.GetToken() != "type")
		{
			return false;
		}
		string token = uSITokenizer.GetToken();
		if (token == string.Empty)
		{
			return false;
		}
		if (token == "button")
		{
			base[tokenName] = new USIOptionButton(tokenName);
		}
		else
		{
			if (uSITokenizer.GetToken() != "default")
			{
				return false;
			}
			switch (token)
			{
			case "check":
			{
				string token4 = uSITokenizer.GetToken();
				if (token4 == string.Empty)
				{
					return false;
				}
				bool defaultValue = token4 == "true";
				base[tokenName] = new USIOptionCheck(tokenName, defaultValue);
				break;
			}
			case "spin":
			{
				if (!USIString.ParseNum(uSITokenizer.GetToken(), out int out_num))
				{
					return false;
				}
				if (uSITokenizer.GetToken() != "min")
				{
					return false;
				}
				if (!USIString.ParseNum(uSITokenizer.GetToken(), out int out_num2))
				{
					return false;
				}
				if (uSITokenizer.GetToken() != "max")
				{
					return false;
				}
				if (!USIString.ParseNum(uSITokenizer.GetToken(), out int out_num3))
				{
					return false;
				}
				base[tokenName] = new USIOptionSpin(tokenName, out_num, out_num2, out_num3);
				break;
			}
			case "combo":
			{
				string token2 = uSITokenizer.GetToken("var");
				if (token2 == string.Empty)
				{
					return false;
				}
				List<string> list = new List<string>();
				while (!(uSITokenizer.GetToken() != "var"))
				{
					string token3 = uSITokenizer.GetToken("var");
					if (token3 == string.Empty)
					{
						break;
					}
					list.Add(token3);
				}
				base[tokenName] = new USIOptionCombo(tokenName, token2, list);
				break;
			}
			case "string":
				base[tokenName] = new USIOptionString(tokenName, uSITokenizer.GetTokenLast());
				break;
			case "filename":
				base[tokenName] = new USIOptionFilename(tokenName, uSITokenizer.GetTokenLast());
				break;
			default:
				return false;
			}
		}
		return true;
	}
}
