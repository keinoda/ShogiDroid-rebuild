using System.Collections.Generic;
using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;
using Java.Lang;
using ShogiGUI;
using Object = Java.Lang.Object;

namespace ShogiDroid.Controls;

public class MainMenuAdapter : BaseAdapter
{
	private Activity activity;

	private List<MainMenuItem> items = new List<MainMenuItem>();

	public override int Count => items.Count;

	public MainMenuAdapter(Activity activity, IList<MainMenuItem> menuItems)
	{
		this.activity = activity;
		foreach (MainMenuItem menuItem in menuItems)
		{
			items.Add(new MainMenuItem(menuItem));
		}
	}

	public override Object GetItem(int position)
	{
		return null;
	}

	public override long GetItemId(int position)
	{
		return items[position].Id;
	}

	public override bool IsEnabled(int position)
	{
		return items[position].Enable;
	}

	public override View GetView(int position, View convertView, ViewGroup parent)
	{
		View view = convertView;
		MainMenuItem mainMenuItem = items[position];
		if (mainMenuItem.TextId == 0)
		{
			view = activity.LayoutInflater.Inflate(Resource.Layout.mainmenudivider, parent, attachToRoot: false);
		}
		else
		{
			if (view == null || view.Id != Resource.Layout.mainmenuitem)
			{
				view = activity.LayoutInflater.Inflate(Resource.Layout.mainmenuitem, parent, attachToRoot: false);
			}
			TextView textView = view.FindViewById<TextView>(Resource.Id.menu_text);
			FontUtil.SetFont(textView);
			textView.Text = activity.GetString(mainMenuItem.TextId);
			if (mainMenuItem.Enable)
			{
				textView.SetTextColor(new Color(ContextCompat.GetColor(activity, Resource.Color.primary_text_default_material_light)));
			}
			else
			{
				textView.SetTextColor(new Color(ContextCompat.GetColor(activity, Resource.Color.primary_text_disabled_material_light)));
			}
		}
		return view;
	}

	public IList<MainMenuItem> GetMenuItems()
	{
		return items;
	}

	public void UpdateGrayout()
	{
		NotifyDataSetInvalidated();
	}
}
