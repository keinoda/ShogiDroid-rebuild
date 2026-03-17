using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;

namespace ShogiDroid;

public class WebViewDialog : DialogFragment
{
	public EventHandler<EventArgs> OKClick;

	public static WebViewDialog NewInstance(int titleId, string path)
	{
		WebViewDialog webViewDialog = new WebViewDialog();
		Bundle bundle = new Bundle();
		bundle.PutInt("TitleId", titleId);
		bundle.PutString("Path", path);
		webViewDialog.Arguments = bundle;
		return webViewDialog;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		int text = base.Arguments.GetInt("TitleId");
		string text2 = base.Arguments.GetString("Path");
		AlertDialog alertDialog = builder.Create();
		View view = base.Activity.LayoutInflater.Inflate(Resource.Layout.webviewdialog, null);
		alertDialog.SetView(view);
		view.FindViewById<TextView>(Resource.Id.DialogTitle).SetText(text);
		view.FindViewById<Button>(Resource.Id.OKButton).Click += delegate(object sender, EventArgs e)
		{
			if (OKClick != null)
			{
				OKClick(sender, e);
			}
			Dismiss();
		};
		view.FindViewById<WebView>(Resource.Id.web_view).LoadUrl("file://" + text2);
		return alertDialog;
	}
}
