using Android.Content;
using Android.Preferences;
using Android.Runtime;
using Android.Util;

namespace ShogiDroid.Controls;

public class ListIntPreference : ListPreference
{
	protected ListIntPreference(System.IntPtr javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer)
	{
	}

	public ListIntPreference(Context context)
		: base(context)
	{
	}

	public ListIntPreference(Context context, IAttributeSet attrs)
		: base(context, attrs)
	{
	}

	public ListIntPreference(Context context, IAttributeSet attrs, int defStyle)
		: base(context, attrs, defStyle)
	{
	}

	protected override bool PersistString(string value)
	{
		int value2 = int.Parse(value);
		return PersistInt(value2);
	}

	protected override string GetPersistedString(string defaultReturnValue)
	{
		int defaultReturnValue2 = 0;
		if (defaultReturnValue != null)
		{
			defaultReturnValue2 = int.Parse(defaultReturnValue);
		}
		return GetPersistedInt(defaultReturnValue2).ToString();
	}
}
