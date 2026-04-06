using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Net;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;
using ShogiGUI.Events;
using ShogiGUI.Models;
using ShogiGUI.Presenters;

namespace ShogiDroid;

[Activity(Label = "EngineInstallActivity", ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/AppTheme")]
public class EngineInstallActivity : ThemedActivity, IEngineInstallView
{
	private const int ZIP_SELECT_REQUEST_CODE = 100;

	private EngineInstallPresenter presenter;

	private ListView engine_listview;

	private MessageBox dialog;

	private SystemUiFlags uiFlags = SystemUiFlags.Fullscreen;

	private MyProgressDialog progressDialog;

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		RequestWindowFeature(WindowFeatures.NoTitle);
		presenter = new EngineInstallPresenter(this);
		presenter.Initialize();
		presenter.EngineList.Add(GetString(Resource.String.EngineInstallInstall_Text));
		presenter.EngineList.Add(GetString(Resource.String.EngineInstallDownload_Text));
		presenter.EngineList.Add(GetString(Resource.String.EngineUninstall_Text));
		InitUI();
	}

	private void InitUI()
	{
		UpdateWindowSettings();
		SetContentView(Resource.Layout.engineinstall);
		FindViewById<Button>(Resource.Id.button_ret).Click += Button_Click;
		engine_listview = FindViewById<ListView>(Resource.Id.engine_list);
		engine_listview.Adapter = new ArrayAdapter<string>(this, 17367043, presenter.EngineList);
		engine_listview.ItemClick += Engine_listview_ItemClick;
	}

	protected override void OnResume()
	{
		base.OnResume();
		presenter.Resume();
	}

	protected override void OnPause()
	{
		base.OnPause();
		presenter.Pause();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		presenter.Destroy();
	}

	public override void OnConfigurationChanged(Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		InitUI();
	}

	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);
		if (hasFocus)
		{
			UpdateWindowSettings();
		}
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
	{
		if (requestCode == 100 && resultCode == Result.Ok && data.Data != null)
		{
			Android.Net.Uri data2 = data.Data;
			string text = Util.GetFileName(this, data2);
			if (string.IsNullOrEmpty(text))
			{
				text = Util.GetPath(this, data2);
			}
			if (string.IsNullOrEmpty(text))
			{
				MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleError_Text, Resource.String.EngineInstallFileSelectError_Text, MessageBox.MBType.MB_OK);
				return;
			}
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
			InstallStart(fileNameWithoutExtension, data2);
		}
	}

	private void Button_Click(object sender, EventArgs e)
	{
		SetResult(Result.Canceled);
		Finish();
	}

	private void Engine_listview_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
	{
		presenter.EngineSelect(e.Position);
	}

	private void InstallStart(string engine_name, Android.Net.Uri zipfile)
	{
		string path = Util.GetPath(this, zipfile);
		if (!string.IsNullOrEmpty(path) && !File.Exists(path))
		{
			path = string.Empty;
		}
		string text = path;
		if (string.IsNullOrEmpty(text))
		{
			text = zipfile.ToString();
		}
		string message = GetString(Resource.String.EngineInstallZipFileName_Text) + "\n" + text + "\n\n" + GetString(Resource.String.EngineInstallInstallFolderName_Text) + "\n" + Path.Combine(Settings.EngineSettings.EngineFolder, engine_name);
		MessageBox messageBox = MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleConfirm_Text, message, MessageBox.MBType.MB_OKCANCEL);
		messageBox.OKClick = (EventHandler<DialogClickEventArgs>)Delegate.Combine(messageBox.OKClick, (EventHandler<DialogClickEventArgs>)delegate
		{
			if (presenter.ExistInstallFolder(engine_name))
			{
				MessageBox messageBox2 = MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleConfirm_Text, Resource.String.EngineInstallExistFolder_Text, MessageBox.MBType.MB_OKCANCEL);
				messageBox2.OKClick = (EventHandler<DialogClickEventArgs>)Delegate.Combine(messageBox2.OKClick, (EventHandler<DialogClickEventArgs>)delegate
				{
					InstallExec(engine_name, new EngineFileUri(zipfile, path));
				});
			}
			else
			{
				InstallExec(engine_name, new EngineFileUri(zipfile, path));
			}
		});
	}

	private void InstallExec(string engine_name, EngineFileUri zipfile)
	{
		progressDialog = MyProgressDialog.NewInstance(Resource.String.MessageBoxTitleProcessing_Text);
		MyProgressDialog myProgressDialog = progressDialog;
		myProgressDialog.CancelClick = (EventHandler<EventArgs>)Delegate.Combine(myProgressDialog.CancelClick, (EventHandler<EventArgs>)delegate
		{
			presenter.InstallCancel();
			dialog = MessageBox.ShowNotice(FragmentManager, Resource.String.EngineInstallCanceling_Text);
			dialog.Cancelable = false;
		});
		progressDialog.Show(FragmentManager, "ProgressDialog");
		Window.AddFlags(WindowManagerFlags.KeepScreenOn);
		presenter.Install(engine_name, zipfile);
	}

	public void MessagePopup(int resid)
	{
		Toast.MakeText(this, resid, ToastLength.Short).Show();
	}

	public void SelectLocalFile()
	{
		Intent intent = new Intent("android.intent.action.GET_CONTENT");
		intent.SetType("application/zip");
		try
		{
			StartActivityForResult(intent, 100);
		}
		catch (ActivityNotFoundException)
		{
			MessagePopup(Resource.String.ActivityNotFound_Text);
		}
	}

	public void DownloadWebPage()
	{
		MessagePopup(Resource.String.EngineDownloadPageUnavailable_Text);
	}

	public void InstallProgress(string filename, int progress)
	{
		if (progressDialog != null)
		{
			progressDialog.Progress = progress;
			progressDialog.SetMessage(filename);
		}
	}

	public void InstallComplete(EngineInstallProgressEventArgs.InstallError error)
	{
		if (progressDialog != null)
		{
			progressDialog.Dismiss();
		}
		Window.ClearFlags(WindowManagerFlags.KeepScreenOn);
		switch (error)
		{
		case EngineInstallProgressEventArgs.InstallError.Cancel:
			MessagePopup(Resource.String.EngineInstallCanceled_Text);
			break;
		case EngineInstallProgressEventArgs.InstallError.NoError:
		{
			string filename = presenter.GetLicense();
			if (filename != string.Empty)
			{
				WebViewDialog dialog = WebViewDialog.NewInstance(Resource.String.EngineInstallLicense_Text, filename);
				dialog.Cancelable = false;
				dialog.Show(FragmentManager, "WebViewDialog");
				WebViewDialog webViewDialog = dialog;
				webViewDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(webViewDialog.OKClick, (EventHandler<EventArgs>)delegate
				{
					filename = presenter.GetReadMe();
					if (filename != string.Empty)
					{
						dialog = WebViewDialog.NewInstance(Resource.String.EngineInstallReadme_Text, filename);
						dialog.Cancelable = false;
						dialog.Show(FragmentManager, "WebViewDialog");
					}
				});
			}
			else
			{
				filename = presenter.GetReadMe();
				if (filename != string.Empty)
				{
					WebViewDialog webViewDialog2 = WebViewDialog.NewInstance(Resource.String.EngineInstallReadme_Text, filename);
					webViewDialog2.Cancelable = false;
					webViewDialog2.Show(FragmentManager, "WebViewDialog");
				}
				else
				{
					MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleConfirm_Text, Resource.String.EngineInstallComplete_Text, MessageBox.MBType.MB_OK);
				}
			}
			break;
		}
		default:
			MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleError_Text, "Install error", MessageBox.MBType.MB_OK);
			break;
		}
	}

	public void ShowSelectUninstallEngine()
	{
		ExternalEngineSelectDialog selectDialog = ExternalEngineSelectDialog.NewInstance(Settings.EngineSettings.GetExternalEngineFolder());
		ExternalEngineSelectDialog externalEngineSelectDialog = selectDialog;
		externalEngineSelectDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(externalEngineSelectDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			string message = GetString(Resource.String.EngineUninstallConfirm_Text) + "\n" + selectDialog.EngineName;
			MessageBox messageBox = MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleConfirm_Text, message, MessageBox.MBType.MB_OKCANCEL);
			messageBox.OKClick = (EventHandler<DialogClickEventArgs>)Delegate.Combine(messageBox.OKClick, (EventHandler<DialogClickEventArgs>)delegate
			{
				presenter.Uninstall(selectDialog.EngineNo, selectDialog.EngineName);
				MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleConfirm_Text, Resource.String.EngineUninstallComplete_Text, MessageBox.MBType.MB_OK);
			});
		});
		selectDialog.Show(FragmentManager, "ExternalEngineSelectDialog");
	}

	private void UpdateWindowSettings()
	{
		if (Settings.AppSettings.DispToolbar)
		{
			uiFlags = SystemUiFlags.ImmersiveSticky;
		}
		else
		{
			uiFlags = SystemUiFlags.Fullscreen;
		}
		if (Settings.AppSettings.DispToolbar)
		{
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
		}
		else
		{
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
		}
		Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiFlags;
	}
}
