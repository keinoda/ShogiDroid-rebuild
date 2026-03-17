using System;
using Android.App;
using Android.Content;
using Android.OS;

namespace ShogiDroid;

public class MessageBox : DialogFragment
{
	public enum MBType
	{
		MB_NONE,
		MB_OK,
		MB_OKCANCEL,
		MB_CANCEL
	}

	private static string tag = typeof(MessageBox).Name;

	private MBType mbtype;

	public EventHandler<DialogClickEventArgs> OKClick;

	public EventHandler<DialogClickEventArgs> CancelClick;

	public static MessageBox ShowError(FragmentManager manager, int messageId)
	{
		MessageBox obj = new MessageBox
		{
			mbtype = MBType.MB_OK
		};
		Bundle bundle = new Bundle();
		bundle.PutInt("IconId", 16843605);
		bundle.PutInt("TitleId", Resource.String.MessageBoxTitleError_Text);
		bundle.PutInt("MessageId", messageId);
		obj.Arguments = bundle;
		obj.Show(manager, tag);
		return obj;
	}

	public static MessageBox ShowError(FragmentManager manager, string message)
	{
		MessageBox obj = new MessageBox
		{
			mbtype = MBType.MB_NONE
		};
		Bundle bundle = new Bundle();
		bundle.PutInt("IconId", 16843605);
		bundle.PutInt("TitleId", Resource.String.MessageBoxTitleError_Text);
		bundle.PutString("Message", message);
		obj.Arguments = bundle;
		obj.Show(manager, tag);
		return obj;
	}

	public static MessageBox ShowNotice(FragmentManager manager, int messageid)
	{
		MessageBox obj = new MessageBox
		{
			mbtype = MBType.MB_OK
		};
		Bundle bundle = new Bundle();
		bundle.PutInt("IconId", 16843605);
		bundle.PutInt("TitleId", Resource.String.MessageBoxTitleNotice_Text);
		bundle.PutInt("MessageId", messageid);
		obj.Arguments = bundle;
		obj.Show(manager, tag);
		return obj;
	}

	public static MessageBox ShowConfirm(FragmentManager manager, int titleId, int messageId, MBType type)
	{
		MessageBox obj = new MessageBox
		{
			mbtype = type
		};
		Bundle bundle = new Bundle();
		bundle.PutInt("IconId", 16843252);
		bundle.PutInt("TitleId", titleId);
		bundle.PutInt("MessageId", messageId);
		obj.Arguments = bundle;
		obj.Show(manager, tag);
		return obj;
	}

	public static MessageBox ShowConfirm(FragmentManager manager, int titleId, string message, MBType type)
	{
		MessageBox obj = new MessageBox
		{
			mbtype = type
		};
		Bundle bundle = new Bundle();
		bundle.PutInt("IconId", 16843252);
		bundle.PutInt("TitleId", titleId);
		bundle.PutString("Message", message);
		obj.Arguments = bundle;
		obj.Show(manager, tag);
		return obj;
	}

	public override Dialog OnCreateDialog(Bundle savedInstanceState)
	{
		AlertDialog.Builder builder = new AlertDialog.Builder(base.Activity);
		int title = base.Arguments.GetInt("TitleId");
		int iconAttribute = base.Arguments.GetInt("IconId");
		string text = base.Arguments.GetString("Message");
		if (text != null)
		{
			builder.SetMessage(text);
		}
		else
		{
			int message = base.Arguments.GetInt("MessageId");
			builder.SetMessage(message);
		}
		builder.SetIconAttribute(iconAttribute);
		builder.SetTitle(title);
		if (mbtype == MBType.MB_OK || mbtype == MBType.MB_OKCANCEL)
		{
			builder.SetPositiveButton(Resource.String.DialogOK_Text, delegate(object sender, DialogClickEventArgs e)
			{
				if (OKClick != null)
				{
					OKClick(sender, e);
				}
			});
		}
		if (mbtype == MBType.MB_OKCANCEL || mbtype == MBType.MB_CANCEL)
		{
			builder.SetNegativeButton(Resource.String.DialogCancel_Text, delegate(object sender, DialogClickEventArgs e)
			{
				if (CancelClick != null)
				{
					CancelClick(sender, e);
				}
			});
		}
		return builder.Create();
	}
}
