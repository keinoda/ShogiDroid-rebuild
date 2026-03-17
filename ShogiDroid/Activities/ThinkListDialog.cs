using System;
using System.Collections.Generic;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiDroid.Controls;
using ShogiGUI.Engine;
using ShogiLib;

namespace ShogiDroid;

public class ThinkListDialog : DialogFragment
{
	public EventHandler<EventArgs> OKClick;

	public EventHandler<ThinkListDialogItemClickEventArgs> ItemClick;

	private ListView listview;

	private IList<PvInfo> pvinfos;

	private ThinkInfiListViewAdapter listviewAdapter;

	private MoveStyle moveStyle;

	public static ThinkListDialog NewInstance(Activity activity, IList<string> commentList, MoveStyle moveStyle)
	{
		ThinkListDialog thinkListDialog = new ThinkListDialog();
		thinkListDialog.LoadComments(commentList);
		thinkListDialog.listviewAdapter = new CommentInfiListViewAdapter(activity);
		thinkListDialog.listviewAdapter.MoveStyle = moveStyle;
		thinkListDialog.listviewAdapter.SetPvInfo(thinkListDialog.pvinfos);
		thinkListDialog.moveStyle = moveStyle;
		return thinkListDialog;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		AlertDialog dialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.thinklistdialog, null);
		dialog.SetView(view);
		listview = view.FindViewById<ListView>(Resource.Id.thinklistview);
		((Button)view.FindViewById(Resource.Id.DialogOKButton)).Click += delegate(object sender, EventArgs e)
		{
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			dialog.Dismiss();
		};
		listview.ItemClick += Listview_ItemClick;
		listview.Adapter = listviewAdapter;
		return dialog;
	}

	private void Listview_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
	{
		if (ItemClick != null)
		{
			PvInfo pvInfo = pvinfos[e.Position];
			ItemClick(this, new ThinkListDialogItemClickEventArgs(e.Position, pvInfo.GetMoves(moveStyle)));
		}
	}

	private void LoadComments(IList<string> comments)
	{
		pvinfos = PvInfos.LoadPvInfos(comments);
	}
}
