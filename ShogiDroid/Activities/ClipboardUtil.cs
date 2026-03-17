using Android.Content;
using ShogiGUI;

namespace ShogiDroid;

public class ClipboardUtil
{
	public static string GetData(Context context)
	{
		ClipData primaryClip = ((ClipboardManager)context.GetSystemService("clipboard")).PrimaryClip;
		string result = string.Empty;
		if (primaryClip != null)
		{
			ClipData.Item itemAt = primaryClip.GetItemAt(0);
			if (itemAt.Text != null && itemAt.Text != string.Empty)
			{
				result = WebKifuFile.GetUrl(itemAt.Text);
			}
		}
		return result;
	}
}
