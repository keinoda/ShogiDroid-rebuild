using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace ShogiGUI;

/// <summary>
/// ヒラギノフォントをアプリ全体に適用するユーティリティ。
/// </summary>
public static class FontUtil
{
	private static Typeface normalTypeface_;
	private static Typeface boldTypeface_;

	public static Typeface Normal
	{
		get
		{
			if (normalTypeface_ == null)
			{
				try
				{
					normalTypeface_ = Typeface.CreateFromAsset(Application.Context.Assets, "font/HiraKakuPro-W3.otf");
				}
				catch
				{
					normalTypeface_ = Typeface.Default;
				}
			}
			return normalTypeface_;
		}
	}

	public static Typeface Bold
	{
		get
		{
			if (boldTypeface_ == null)
			{
				try
				{
					boldTypeface_ = Typeface.CreateFromAsset(Application.Context.Assets, "font/HiraKakuPro-W6.otf");
				}
				catch
				{
					boldTypeface_ = Typeface.DefaultBold;
				}
			}
			return boldTypeface_;
		}
	}

	/// <summary>
	/// ViewGroup内のすべてのTextViewにヒラギノフォントを再帰的に適用する。
	/// </summary>
	public static void ApplyFont(View view)
	{
		if (view is TextView tv)
		{
			if (tv.Typeface != null && tv.Typeface.IsBold)
				tv.SetTypeface(Bold, TypefaceStyle.Normal);
			else
				tv.SetTypeface(Normal, TypefaceStyle.Normal);
		}
		else if (view is ViewGroup vg)
		{
			for (int i = 0; i < vg.ChildCount; i++)
			{
				ApplyFont(vg.GetChildAt(i));
			}
		}
	}
}
