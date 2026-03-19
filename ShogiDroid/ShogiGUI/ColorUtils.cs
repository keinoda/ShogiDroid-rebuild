using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using AndroidX.Core.Content;

namespace ShogiGUI;

public static class ColorUtils
{
	public static Color Get(Context context, int colorResId)
	{
		return new Color(ContextCompat.GetColor(context, colorResId));
	}

	public static bool IsDarkMode(Context context)
	{
		var uiMode = context.Resources.Configuration.UiMode & UiMode.NightMask;
		return uiMode == UiMode.NightYes;
	}
}
