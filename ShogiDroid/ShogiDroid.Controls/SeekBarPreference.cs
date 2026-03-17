using Math = System.Math;
using System;
using Android.Content;
using Android.Content.Res;
using Android.Preferences;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace ShogiDroid.Controls;

public class SeekBarPreference : Preference
{
	private SeekBar seekBar;

	private TextView titleText;

	private TextView valText;

	private int maxValue = 100;

	private int curValue = 100;

	private int defValue = 100;

	protected SeekBarPreference(IntPtr javaReference, JniHandleOwnership transfer)
		: base(javaReference, transfer)
	{
	}

	public SeekBarPreference(Context context)
		: base(context)
	{
		init(null, 0);
	}

	public SeekBarPreference(Context context, IAttributeSet attrs)
		: base(context, attrs)
	{
		init(attrs, 0);
	}

	public SeekBarPreference(Context context, IAttributeSet attrs, int defStyle)
		: base(context, attrs, defStyle)
	{
		init(attrs, defStyle);
	}

	private void init(IAttributeSet attrs, int defStyle)
	{
		TypedArray typedArray = Context.ObtainStyledAttributes(attrs, Resource.Styleable.SeekBar, defStyle, 0);
		maxValue = typedArray.GetInt(0, 100);
		defValue = typedArray.GetInt(1, 100);
		typedArray.Recycle();
	}

	protected override bool ShouldPersist()
	{
		return base.ShouldPersist();
	}

	protected override View OnCreateView(ViewGroup parent)
	{
		View view = ((LayoutInflater)Context.GetSystemService("layout_inflater")).Inflate(Resource.Layout.seekbarpreference, parent, attachToRoot: false);
		seekBar = view.FindViewById<SeekBar>(Resource.Id.preference_seekbar);
		seekBar.Max = maxValue;
		seekBar.Progress = curValue;
		seekBar.ProgressChanged += SeekBar_ProgressChanged;
		titleText = view.FindViewById<TextView>(Resource.Id.preference_seekbar_title);
		titleText.Text = base.Title;
		valText = view.FindViewById<TextView>(Resource.Id.preference_seekbar_value);
		valText.Text = curValue + "%";
		return view;
	}

	protected override Java.Lang.Object OnGetDefaultValue(TypedArray a, int index)
	{
		int val = a.GetInt(index, defValue);
		val = Math.Min(maxValue, Math.Max(0, val));
		return val;
	}

	protected override void OnSetInitialValue(bool restorePersistedValue, Java.Lang.Object defaultValue)
	{
		int value;
		if (restorePersistedValue)
		{
			value = GetPersistedInt(defValue);
		}
		else
		{
			value = (int)defaultValue;
			PersistInt(value);
		}
		curValue = value;
	}

	private void SeekBar_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
	{
		if (curValue != e.Progress)
		{
			curValue = e.Progress;
			valText.Text = curValue + "%";
			ISharedPreferencesEditor sharedPreferencesEditor = SharedPreferences.Edit();
			sharedPreferencesEditor.PutInt(Key, curValue);
			sharedPreferencesEditor.Commit();
		}
	}
}
