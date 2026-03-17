using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using ShogiGUI;

namespace ShogiDroid;

[Activity(Label = "SelectFolderActivity", ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/Theme.AppCompat.Light")]
public class SelectFolderActivity : Activity
{
	private string path;

	private string default_path;

	private string[] file_list;

	private SystemUiFlags uiFlags = SystemUiFlags.Fullscreen;

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		path = Intent.GetStringExtra("path");
		default_path = Intent.GetStringExtra("default");
		if (path == string.Empty)
		{
			path = default_path;
		}
		RequestWindowFeature(WindowFeatures.NoTitle);
		InitUI();
	}

	private void InitUI()
	{
		UpdateWindowSettings();
		SetContentView(Resource.Layout.selectfolder);
		TextView textview = FindViewById<TextView>(Resource.Id.SelectFolderDialogPath);
		textview.Text = path;
		FindViewById<Button>(Resource.Id.DialogOKButton).Click += delegate
		{
			Intent intent = new Intent();
			intent.PutExtra("path", path);
			SetResult(Result.Ok, intent);
			Finish();
		};
		FindViewById<Button>(Resource.Id.DialogCancelButton).Click += delegate
		{
			SetResult(Result.Canceled);
			Finish();
		};
		file_list = LoadFileList(path);
		ListView listview = FindViewById<ListView>(Resource.Id.SelectFolderDialogListView);
		listview.Adapter = new ArrayAdapter<string>(this, 17367043, file_list);
		listview.ItemClick += delegate(object sender, AdapterView.ItemClickEventArgs e)
		{
			path = Path.Combine(path, file_list[e.Position]);
			textview.Text = path;
			file_list = LoadFileList(path);
			listview.Adapter = new ArrayAdapter<string>(this, 17367043, file_list);
		};
		FindViewById<Button>(Resource.Id.SelectFolderDialogUp).Click += delegate
		{
			if (Path.GetFileName(path) != string.Empty)
			{
				path = Path.GetDirectoryName(path);
				textview.Text = path;
				file_list = LoadFileList(path);
				listview.Adapter = new ArrayAdapter<string>(this, 17367043, file_list);
			}
		};
		FindViewById<Button>(Resource.Id.SelectFolderDialogDefault).Click += delegate
		{
			path = default_path;
			textview.Text = path;
			file_list = LoadFileList(path);
			listview.Adapter = new ArrayAdapter<string>(this, 17367043, file_list);
		};
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

	private string[] LoadFileList(string path)
	{
		try
		{
			return (from filename in Directory.GetDirectories(path, "*")
				select Path.GetFileName(filename)).ToArray();
		}
		catch
		{
			return new string[0];
		}
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
