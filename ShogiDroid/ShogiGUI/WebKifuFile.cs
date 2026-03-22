using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Hnx8.ReadJEnc;

namespace ShogiGUI;

public class WebKifuFile
{
	public static byte[] Load(string address)
	{
		try
		{
			using WebClient webClient = new WebClient();
			return webClient.DownloadData(address);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public static string Load(string address, Encoding encoding)
	{
		string empty = string.Empty;
		try
		{
			using WebClient webClient = new WebClient();
			using StreamReader streamReader = new StreamReader(webClient.OpenRead(address), encoding);
			return streamReader.ReadToEnd();
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public static string GetDirectoryName(string path)
	{
		return Path.GetDirectoryName(path).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace(":", ":/");
	}

	public static string LoadKifu(string address)
	{
		string text;
		try
		{
			byte[] array = Load(address);
			ReadJEnc.JP.GetEncoding(array, array.Length, out text);
			if (text == null)
			{
				text = string.Empty;
			}
			if (IsHtml(text))
			{
				string warsKifu;
				if ((warsKifu = GetKishinKifu(text)) != string.Empty)
				{
					text = warsKifu;
				}
				else if ((warsKifu = GetWarsKifu2(text)) != string.Empty)
				{
					text = warsKifu;
				}
				else if ((warsKifu = GetWarsKifu(text)) != string.Empty)
				{
					text = warsKifu;
				}
				else
				{
					string kifAddress = GetKifAddress(text, address);
					if (kifAddress != string.Empty)
					{
						array = Load(kifAddress);
						ReadJEnc.JP.GetEncoding(array, array.Length, out text);
					}
				}
			}
		}
		catch (Exception ex)
		{
			throw ex;
		}
		return text;
	}

	public static bool IsHtml(string html)
	{
		return Regex.IsMatch(html, "<html.*</html>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
	}

	public static string GetKifAddress(string html, string address)
	{
		Uri uri = new Uri(address);
		string empty = string.Empty;
		string text = string.Empty;
		Match match;
		if ((match = Regex.Match(html, "<PARAM[ ]+NAME[ ]*=[ ]*KIFU[ ]+VALUE=\"(.*)\">", RegexOptions.IgnoreCase | RegexOptions.Multiline)).Success)
		{
			text = match.Groups[1].Value;
		}
		else if ((match = Regex.Match(html, "FlashVars[ ]*=[ ]*\"kifu[ ]*=[ ]*(.*\\.kif)")).Success)
		{
			text = match.Groups[1].Value;
		}
		else if ((match = Regex.Match(html, "addVariable[ ]*\\([ ]*\"kifu\"[ ]*,[ ]*\"(.*\\.kif[uU]*)")).Success)
		{
			text = match.Groups[1].Value;
		}
		else if ((match = Regex.Match(html, "kifu[ ]*=.*\\.kif[uU]*")).Success)
		{
			text = match.Groups[1].Value;
		}
		else if ((match = Regex.Match(html, "\"(data/.*\\.kif)")).Success)
		{
			text = match.Groups[1].Value;
		}
		else if ((match = Regex.Match(html, "KIF_FILE_NAME[ ]*=[ ]*\"(.*)\"")).Success)
		{
			text = match.Groups[1].Value;
		}
		if (text != string.Empty && text[0] == '/')
		{
			return uri.GetLeftPart(UriPartial.Authority) + text;
		}
		return GetDirectoryName(address) + "/" + text;
	}

	public static string GetWarsKifu(string html)
	{
		string text = string.Empty;
		string text2 = string.Empty;
		Match match;
		if ((match = Regex.Match(html, "receiveMove\\(\\\"(.+)\\\"", RegexOptions.IgnoreCase | RegexOptions.Multiline)).Success)
		{
			text2 = match.Groups[1].Value;
		}
		if (text2 != string.Empty)
		{
			if ((match = Regex.Match(html, "<title>(.+)</title>")).Success)
			{
				if ((match = Regex.Match(match.Groups[1].Value, ".+\\((.+)対(.+)\\)")).Success)
				{
					text = text + "N+" + match.Groups[1].Value + "\n";
					text = text + "N-" + match.Groups[2].Value + "\n";
				}
				else
				{
					text += "N+\n";
					text += "N-\n";
				}
			}
			if ((match = Regex.Match(html, "name:.*\\\".+-.+-(.*)_(.*)\\\"", RegexOptions.IgnoreCase)).Success)
			{
				int result = 0;
				int result2 = 0;
				if (int.TryParse(match.Groups[1].Value, out result))
				{
					text = text + "$START_TIME:" + result / 10000;
					result -= result / 10000 * 10000;
					text += $"/{result / 100:D2}";
					result -= result / 100 * 100;
					text += $"/{result:D2}";
				}
				if (int.TryParse(match.Groups[2].Value, out result2))
				{
					text += $" {result2 / 10000:D2}";
					result2 -= result2 / 10000 * 10000;
					text += $":{result2 / 100:D2}";
					result2 -= result2 / 100 * 100;
					text += $":{result2:D2}";
				}
				text += "\n";
			}
			if ((match = Regex.Match(html, "gtype:.*\\\"(.*)\\\"", RegexOptions.IgnoreCase)).Success)
			{
				if (match.Groups[1].Value == "sb")
				{
					text += "$EVENT:将棋ウォーズ(3分切れ負け)\n";
					text += "$TIME_LIMIT:00:03+00\n";
				}
				else if (match.Groups[1].Value == "s1")
				{
					text += "$EVENT:将棋ウォーズ(10秒将棋)\n";
					text += "$TIME_LIMIT:00:00+10\n";
				}
				else
				{
					text += "$EVENT:将棋ウォーズ(10分切れ負け)\n";
					text += "$TIME_LIMIT:00:10+00\n";
				}
			}
			else
			{
				text += "$EVENT:将棋ウォーズ(10分切れ負け)\n";
				text += "$TIME_LIMIT:00:10+00\n";
			}
			text += "PI\n";
			text += "+\n";
			text2 = Regex.Replace(text2, "\t([SGD])", "\t%$1");
			text2 = text2.Replace(',', '\n');
			text += text2.Replace('\t', '\n');
		}
		return text;
	}

	public static string GetWarsKifu2(string html)
	{
		Match match;
		if (!(match = Regex.Match(html, "data-react-props=\\\"(.+)\\\"", RegexOptions.IgnoreCase | RegexOptions.Multiline)).Success)
		{
			return string.Empty;
		}
		string value = match.Groups[1].Value;
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		value = value.Replace("&quot", "'");
		value = value.Replace(";:", "\t");
		value = value.Replace(";", string.Empty);
		int result = 100;
		int result2 = 100;
		using MemoryStream memoryStream = new MemoryStream();
		using (StreamWriter streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, 1024, leaveOpen: true))
		{
			match = Regex.Match(value, "'name'\\\t'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				streamWriter.WriteLine("$START_TIME:{0}", NameToDate(match.Groups[1].Value));
			}
			match = Regex.Match(value, "'sente_dan'\\\t([^',]+),", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				int.TryParse(match.Groups[1].Value, out result);
			}
			match = Regex.Match(value, "'gote_dan'\\\t([^',]+),", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				int.TryParse(match.Groups[1].Value, out result2);
			}
			match = Regex.Match(value, "'sente'\\\t'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				streamWriter.WriteLine("N+{0} {1}", match.Groups[1].Value, DanToString(result));
			}
			match = Regex.Match(value, "'gote'\\\t'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				streamWriter.WriteLine("N-{0} {1}", match.Groups[1].Value, DanToString(result2));
			}
			int num = 600;
			int num2 = 600;
			string text = string.Empty;
			match = Regex.Match(value, "'gtype'\\\t'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				text = match.Groups[1].Value;
			}
			switch (text)
			{
			case "s1":
				num = 3600;
				num2 = 3600;
				streamWriter.WriteLine("$EVENT:将棋ウォーズ(10秒将棋)");
				break;
			case "sb":
				num = 180;
				num2 = 180;
				streamWriter.WriteLine("$EVENT:将棋ウォーズ(3分切れ負け)");
				break;
			case "a":
				num = 120;
				num2 = 120;
				streamWriter.WriteLine("$EVENT:将棋ウォーズ(2分切れ負け)");
				break;
			default:
				streamWriter.WriteLine("$EVENT:将棋ウォーズ(10分切れ負け)");
				break;
			}
			int result3 = 0;
			string text2 = string.Empty;
			match = Regex.Match(value, "'handicap'\\\t([^',]+),", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				int.TryParse(match.Groups[1].Value, out result3);
			}
			if (result3 == 0)
			{
				streamWriter.WriteLine("PI");
			}
			else
			{
				match = Regex.Match(value, "'init_sfen_position'\\\t([^',]+),", RegexOptions.IgnoreCase | RegexOptions.Multiline);
				if (match.Success)
				{
					text2 = match.Groups[1].Value;
				}
				if (text2 != string.Empty)
				{
					Console.WriteLine("PS", text2);
				}
				else
				{
					streamWriter.WriteLine("PI");
				}
			}
			string arg = string.Empty;
			match = Regex.Match(value, "'result'\\\t'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				arg = match.Groups[1].Value;
			}
			match = Regex.Match(value, "'moves'\\\t\\[(.+)]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				string[] array = match.Groups[1].Value.Split('}');
				int num3 = 1;
				bool flag = false;
				string[] array2 = array;
				foreach (string text3 in array2)
				{
					if (string.IsNullOrWhiteSpace(text3))
					{
						break;
					}
					match = Regex.Match(text3, "'m'\\\t'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Multiline);
					if (!match.Success)
					{
						flag = true;
						break;
					}
					streamWriter.WriteLine(match.Groups[1].Value);
					match = Regex.Match(text3, "'t'\\\t([^',]+),", RegexOptions.IgnoreCase | RegexOptions.Multiline);
					if (match.Success)
					{
						int result4 = 0;
						int.TryParse(match.Groups[1].Value, out result4);
						if ((num3 & 1) == 1)
						{
							streamWriter.WriteLine("T{0}", num - result4);
							num = result4;
						}
						else
						{
							streamWriter.WriteLine("T{0}", num2 - result4);
							num2 = result4;
						}
					}
					num3++;
				}
				if (!flag)
				{
					streamWriter.WriteLine("%{0}", arg);
				}
			}
		}
		memoryStream.Position = 0L;
		using StreamReader streamReader = new StreamReader(memoryStream);
		return streamReader.ReadToEnd();
	}

	/// <summary>
	/// 棋神アナリティクスのHTMLからKIF形式棋譜を抽出
	/// </summary>
	public static string GetKishinKifu(string html)
	{
		// "kif" フィールドにエスケープされたKIF棋譜が格納されている
		var match = Regex.Match(html, @"""kif""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Singleline);
		if (!match.Success)
			return string.Empty;

		string escaped = match.Groups[1].Value;
		// JavaScriptのエスケープを解除
		string kif = Regex.Unescape(escaped);
		return kif;
	}

	public static string GetUrl(string str)
	{
		string result = string.Empty;
		Match match;
		if ((match = Regex.Match(str, "(http\\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)).Success)
		{
			result = match.Groups[1].Value;
		}
		return result;
	}

	public static bool IsUrl(string str)
	{
		bool result = false;
		string text = string.Empty;
		using (StringReader stringReader = new StringReader(str))
		{
			text = stringReader.ReadLine();
		}
		text = text.TrimStart();
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		if (text[0] != '#' && text[0] != '*' && Regex.Match(text, "(http\\S+)", RegexOptions.IgnoreCase).Success)
		{
			result = true;
		}
		return result;
	}

	public static string DanToString(int dan)
	{
		char[] array = new char[9] { '初', '二', '三', '四', '五', '六', '七', '八', '九' };
		if (dan >= 0 && dan < 9)
		{
			return $"{array[dan]}段";
		}
		if (dan < 0)
		{
			dan = -dan;
			return $"{dan}級";
		}
		return string.Empty;
	}

	public static string NameToDate(string name)
	{
		string[] array = name.Split('-');
		if (array.Length < 2 && array[2].Length < 15)
		{
			return string.Empty;
		}
		string obj = array[2];
		string text = obj.Substring(0, 4);
		string text2 = obj.Substring(4, 2);
		string text3 = obj.Substring(6, 2);
		string text4 = obj.Substring(9, 2);
		string text5 = obj.Substring(11, 2);
		string text6 = obj.Substring(13, 2);
		return $"{text}/{text2}/{text3} {text4}:{text5}:{text6}";
	}
}
