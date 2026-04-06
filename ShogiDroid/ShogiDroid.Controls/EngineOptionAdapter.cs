using Math = System.Math;
using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Java.Lang;
using ShogiGUI;
using ShogiGUI.Engine;
namespace ShogiDroid.Controls;

public class EngineOptionAdapter : BaseAdapter
{
	private class OptionSpinnerAdapter : ArrayAdapter<string>
	{
		private string defaultString;

		private string selString;

		public string SelectString
		{
			get
			{
				return selString;
			}
			set
			{
				selString = value;
			}
		}

		public OptionSpinnerAdapter(Context context, int resource, IList<string> strings, string val, string def)
			: base(context, resource, strings)
		{
			defaultString = def;
			selString = val;
		}

		public override View GetDropDownView(int position, View convertView, ViewGroup parent)
		{
			if (convertView == null)
			{
				convertView = LayoutInflater.From(Context).Inflate(17367049, parent, attachToRoot: false);
			}
			TextView textView = (TextView)convertView;
			textView.Text = GetItem(position);
			if (textView.Text == selString)
			{
				textView.SetTextColor(ColorUtils.Get(Context, Resource.Color.option_change_text_color));
			}
			else
			{
				textView.SetTextColor(ColorUtils.Get(Context, Resource.Color.option_normal_text_color));
			}
			return convertView;
		}

		public override View GetView(int position, View convertView, ViewGroup parent)
		{
			if (convertView == null)
			{
				convertView = LayoutInflater.From(Context).Inflate(17367048, parent, attachToRoot: false);
			}
			TextView textView = (TextView)convertView;
			textView.Text = GetItem(position);
			if (textView.Text != defaultString)
			{
				textView.SetTextColor(ColorUtils.Get(Context, Resource.Color.option_change_text_color));
			}
			else
			{
				textView.SetTextColor(ColorUtils.Get(Context, Resource.Color.option_normal_text_color));
			}
			return convertView;
		}
	}

	private Activity activity;

	private List<USIOption> optionlist = new List<USIOption>();

	public IList<USIOption> OptionList => optionlist;

	public override int Count => optionlist.Count;

	public event EventHandler<OptionButtonEventArgs> ButtonClick;

	public EngineOptionAdapter(Activity activity, USIOptions options)
	{
		this.activity = activity;
		foreach (KeyValuePair<string, USIOption> option in options)
		{
			optionlist.Add(option.Value.Clone());
		}
	}

	public override Java.Lang.Object GetItem(int position)
	{
		return null;
	}

	public override long GetItemId(int position)
	{
		return position;
	}

	public override View GetView(int position, View convertView, ViewGroup parent)
	{
		View view = convertView;
		USIOption uSIOption = optionlist[position];
		switch (uSIOption.Type)
		{
		case USIOptionType.CHECK:
		{
			USIOptionCheck uSIOptionCheck = (USIOptionCheck)uSIOption;
			view = activity.LayoutInflater.Inflate(Resource.Layout.engineoptioncheckbox, parent, attachToRoot: false);
			CheckBox checkBox = view.FindViewById<CheckBox>(Resource.Id.OptionCheckBox);
			checkBox.Checked = uSIOptionCheck.Value;
			checkBox.Text = uSIOptionCheck.Name;
			checkBox.Tag = position;
			if (uSIOptionCheck.Value != uSIOptionCheck.DefaultValue)
			{
				checkBox.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_change_text_color));
			}
			else
			{
				checkBox.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_normal_text_color));
			}
			checkBox.Click += Checkbox_Click;
			view.FindViewById<TextView>(Resource.Id.OptionSummary).Text = activity.GetString(Resource.String.Default_Text) + ":" + uSIOptionCheck.DefaultValue;
			break;
		}
		case USIOptionType.SPIN:
		{
			USIOptionSpin uSIOptionSpin = (USIOptionSpin)uSIOption;
			view = activity.LayoutInflater.Inflate(Resource.Layout.engineoptionspin, parent, attachToRoot: false);
			view.FindViewById<TextView>(Resource.Id.OptionName).Text = uSIOption.Name;
			EditText editText2 = view.FindViewById<EditText>(Resource.Id.OptionNumber);
			editText2.Tag = position;
			editText2.Text = uSIOptionSpin.ValueToString();
			if (uSIOptionSpin.Value != uSIOptionSpin.DefaultValue)
			{
				editText2.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_change_text_color));
			}
			else
			{
				editText2.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_normal_text_color));
			}
			editText2.FocusChange += Spin_FocusChange;
			view.FindViewById<TextView>(Resource.Id.OptionSummary).Text = activity.GetString(Resource.String.Min_Text) + ":" + uSIOptionSpin.Min + " " + activity.GetString(Resource.String.Max_Text) + ":" + uSIOptionSpin.Max + " " + activity.GetString(Resource.String.Default_Text) + ":" + uSIOptionSpin.DefaultValue;
			break;
		}
		case USIOptionType.COMBO:
		{
			USIOptionCombo uSIOptionCombo = (USIOptionCombo)uSIOption;
			view = activity.LayoutInflater.Inflate(Resource.Layout.engineoptioncombo, parent, attachToRoot: false);
			view.FindViewById<TextView>(Resource.Id.OptionName).Text = uSIOption.Name;
			Spinner spinner = view.FindViewById<Spinner>(Resource.Id.OptionCombo);
			spinner.Tag = position;
			ArrayAdapter adapter = new OptionSpinnerAdapter(activity, 17367048, uSIOptionCombo.ComboValues, uSIOptionCombo.Value, uSIOptionCombo.DefaultValue);
			spinner.Adapter = adapter;
			int num = uSIOptionCombo.ComboValues.IndexOf(uSIOptionCombo.Value);
			if (num >= 0)
			{
				spinner.SetSelection(num);
			}
			spinner.ItemSelected += Spinner_ItemSelected;
			view.FindViewById<TextView>(Resource.Id.OptionSummary).Text = activity.GetString(Resource.String.Default_Text) + ":" + uSIOptionCombo.DefaultValue;
			break;
		}
		case USIOptionType.STRING:
		case USIOptionType.FILENAME:
		{
			USIOptionString uSIOptionString = (USIOptionString)uSIOption;
			view = activity.LayoutInflater.Inflate(Resource.Layout.engineoptionstring, parent, attachToRoot: false);
			view.FindViewById<TextView>(Resource.Id.OptionName).Text = uSIOption.Name;
			EditText editText = view.FindViewById<EditText>(Resource.Id.OptionString);
			editText.Text = uSIOptionString.Value;
			editText.Tag = position;
			if (uSIOptionString.Value != uSIOptionString.DefaultValue)
			{
				editText.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_change_text_color));
			}
			else
			{
				editText.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_normal_text_color));
			}
			editText.FocusChange += String_FocusChange;
			view.FindViewById<TextView>(Resource.Id.OptionSummary).Text = activity.GetString(Resource.String.Default_Text) + ":" + uSIOptionString.DefaultValue;
			break;
		}
		case USIOptionType.BUTTON:
		{
			Button button = new Button(activity);
			button.Id = Resource.Id.EngineOptionButton;
			button.Text = uSIOption.Name;
			button.Tag = position;
			button.Click += Button_Click;
			view = button;
			break;
		}
		}
		return view;
	}

	private void String_FocusChange(object sender, View.FocusChangeEventArgs e)
	{
		EditText editText = (EditText)sender;
		int index = (int)editText.Tag;
		if (!e.HasFocus)
		{
			USIOptionString uSIOptionString = (USIOptionString)optionlist[index];
			uSIOptionString.SetValue(editText.Text);
			if (uSIOptionString.Value != uSIOptionString.DefaultValue)
			{
				editText.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_change_text_color));
			}
			else
			{
				editText.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_normal_text_color));
			}
		}
	}

	private void Spinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
	{
		Spinner obj = (Spinner)sender;
		int index = (int)obj.Tag;
		USIOptionCombo uSIOptionCombo = (USIOptionCombo)optionlist[index];
		uSIOptionCombo.SetValue(uSIOptionCombo.ComboValues[e.Position]);
		((OptionSpinnerAdapter)obj.Adapter).SelectString = uSIOptionCombo.Value;
	}

	private void Spin_FocusChange(object sender, View.FocusChangeEventArgs e)
	{
		EditText editText = (EditText)sender;
		int index = (int)editText.Tag;
		if (e.HasFocus)
		{
			return;
		}
		USIOptionSpin uSIOptionSpin = (USIOptionSpin)optionlist[index];
		if (int.TryParse(editText.Text, out var result))
		{
			result = Math.Min(uSIOptionSpin.Max, Math.Max(uSIOptionSpin.Min, result));
			uSIOptionSpin.SetValue(result);
			if (uSIOptionSpin.Value != uSIOptionSpin.DefaultValue)
			{
				editText.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_change_text_color));
			}
			else
			{
				editText.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_normal_text_color));
			}
		}
	}

	private void Checkbox_Click(object sender, EventArgs e)
	{
		CheckBox checkBox = (CheckBox)sender;
		int index = (int)checkBox.Tag;
		USIOptionCheck uSIOptionCheck = (USIOptionCheck)optionlist[index];
		uSIOptionCheck.SetValue(checkBox.Checked);
		if (uSIOptionCheck.Value != uSIOptionCheck.DefaultValue)
		{
			checkBox.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_change_text_color));
		}
		else
		{
			checkBox.SetTextColor(ColorUtils.Get(activity, Resource.Color.option_normal_text_color));
		}
	}

	private void Button_Click(object sender, EventArgs e)
	{
		int index = (int)((Button)sender).Tag;
		USIOptionButton uSIOptionButton = (USIOptionButton)optionlist[index];
		if (this.ButtonClick != null)
		{
			this.ButtonClick(this, new OptionButtonEventArgs(uSIOptionButton.Name));
		}
	}
}
