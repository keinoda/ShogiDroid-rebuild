using Android.App;
using Android.Graphics;
using Android.OS;
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
	private static bool initialized_;

	public static Typeface Normal
	{
		get
		{
			EnsureInit();
			return normalTypeface_;
		}
	}

	public static Typeface Bold
	{
		get
		{
			EnsureInit();
			return boldTypeface_;
		}
	}

	private static void EnsureInit()
	{
		if (initialized_) return;
		initialized_ = true;
		try
		{
			normalTypeface_ = Typeface.CreateFromAsset(Application.Context.Assets, "font/HiraKakuPro-W3.otf");
		}
		catch
		{
			normalTypeface_ = Typeface.Default;
		}
		try
		{
			boldTypeface_ = Typeface.CreateFromAsset(Application.Context.Assets, "font/HiraKakuPro-W6.otf");
		}
		catch
		{
			boldTypeface_ = Typeface.DefaultBold;
		}
	}

	/// <summary>
	/// ViewGroup内のすべてのTextViewにヒラギノフォントを再帰的に適用する。
	/// </summary>
	public static void ApplyFont(View view)
	{
		if (view == null) return;
		if (view is TextView tv)
		{
			if (tv.Typeface != null && tv.Typeface.IsBold)
				tv.SetTypeface(Bold, TypefaceStyle.Bold);
			else
				tv.SetTypeface(Normal, TypefaceStyle.Normal);
		}
		if (view is ViewGroup vg)
		{
			for (int i = 0; i < vg.ChildCount; i++)
			{
				ApplyFont(vg.GetChildAt(i));
			}
		}
	}

	/// <summary>
	/// TextViewに適切なヒラギノフォントを設定する。
	/// </summary>
	public static void SetFont(TextView tv)
	{
		if (tv == null) return;
		if (tv.Typeface != null && tv.Typeface.IsBold)
			tv.SetTypeface(Bold, TypefaceStyle.Bold);
		else
			tv.SetTypeface(Normal, TypefaceStyle.Normal);
	}

	/// <summary>
	/// ActivityLifecycleCallbacksを登録して全Activityにフォントを自動適用する。
	/// Application.OnCreate()で呼ぶこと。
	/// </summary>
	public static void RegisterGlobal(Application app)
	{
		app.RegisterActivityLifecycleCallbacks(new FontLifecycleCallbacks());
	}

	private class FontLifecycleCallbacks : Java.Lang.Object, Application.IActivityLifecycleCallbacks
	{
		public void OnActivityCreated(Activity activity, Bundle savedInstanceState) { }
		public void OnActivityDestroyed(Activity activity) { }
		public void OnActivityPaused(Activity activity) { }
		public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }
		public void OnActivityStarted(Activity activity) { }
		public void OnActivityStopped(Activity activity) { }

		public void OnActivityResumed(Activity activity)
		{
			var rootView = activity.FindViewById(Android.Resource.Id.Content);
			if (rootView != null)
			{
				ApplyFont(rootView);
			}
		}
	}
}
