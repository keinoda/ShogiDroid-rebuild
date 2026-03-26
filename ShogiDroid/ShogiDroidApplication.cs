using Android.App;
using Android.Runtime;
using ShogiGUI;
using System;

namespace ShogiDroid;

[Application]
public class ShogiDroidApplication : Application
{
	public ShogiDroidApplication(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
	{
	}

	public override void OnCreate()
	{
		base.OnCreate();
		Settings.Load();
		ThemeHelper.ApplyTheme(ThemeHelper.GetThemeMode());
		FontUtil.RegisterGlobal(this);
	}
}
