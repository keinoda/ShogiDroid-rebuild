using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Orientation = Android.Content.Res.Orientation;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.DrawerLayout.Widget;
using AndroidX.ViewPager.Widget;
using Java.IO;
using Java.Interop;
using Java.Util;
using ShogiDroid.Controls;
using ShogiDroid.Controls.ShogiBoard;
using ShogiGUI;
using GameMode = ShogiGUI.GameMode;
using ShogiGUI.Engine;
using ShogiGUI.Events;
using ShogiGUI.Presenters;
using ShogiLib;

namespace ShogiDroid;

[Activity(Label = "ShogiDroid", MainLauncher = true, Icon = "@drawable/shogidroid_icon", WindowSoftInputMode = SoftInput.AdjustNothing, ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/Theme.AppCompat.Light")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" }, DataScheme = "http", DataHost = "live.shogi.or.jp", DataPathPattern = "/.*/kifu/.*")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" }, DataScheme = "http", DataHost = "*", DataPathPattern = "/.*.kif")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" }, DataScheme = "http", DataHost = "*", DataPathPattern = "/.*.ki2")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" }, DataScheme = "http", DataHost = "*", DataPathPattern = ".*\\\\.csa")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" }, DataScheme = "http", DataHost = "wdoor.c.u-tokyo.ac.jp", DataPathPattern = ".*csa")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataScheme = "file", DataHost = "*", DataPathPattern = "/.*.kif")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataScheme = "file", DataHost = "*", DataPathPattern = "/.*.ki2")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataScheme = "file", DataHost = "*", DataPathPattern = ".*\\\\.csa")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataMimeType = "*/*", DataScheme = "file", DataHost = "*", DataPathPattern = "/.*.kif")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataMimeType = "*/*", DataScheme = "file", DataHost = "*", DataPathPattern = "/.*.ki2")]
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataMimeType = "*/*", DataScheme = "file", DataHost = "*", DataPathPattern = ".*\\\\.csa")]
[IntentFilter(new string[] { "android.intent.action.SEND" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataMimeType = "message/rfc822")]
public class MainActivity : Activity, IMainView, ActivityCompat.IOnRequestPermissionsResultCallback, IJavaObject, IDisposable, IJavaPeerable
{
	private delegate void funcSaveNotationOkDelegate(string filename);

	private CommandMap commands = new CommandMap();

	private const int NOTATION_SELECT_REQUEST_CODE = 100;

	private const int NOTATION_SEND_REQUEST_CODE = 101;

	private const int SETTINGS_CODE = 102;

	private const int EDIT_BOARD_CODE = 103;

	private const int ENGINE_OPTIONS_CODE = 104;

	private const int SELECT_ENGINE_FOLDER_CODE = 105;

	private const int JOINT_BOARD_CODE = 106;

	private const int ENGINE_INSTALL_REQUEST_CODE = 107;

	private const int WARS_GAME_RESULT_CODE = 108;

	private const int CAMERA_READ_CODE = 109;

	private ShogiBoard shogiBoard;

	private EvalBar evalBar;

	private ImageButton prevButton;

	private ImageButton nextButton;

	private ImageButton firstButton;

	private ImageButton lastButton;

	private View analyzButton;

	private TextView analyzeText;

	private bool isAnalyzeActive;

	private DateTime analyzeStartTime;

	private System.Timers.Timer analyzeTimer;

	private ImageButton reverseButton;

	private ImageButton menuButton;

	private ImageButton inputCancelButton;

	private TextView stateText;

	private DrawerLayout drawerLayout;

	private TextView topName;

	private TextView topTime;

	private TextView bottomName;

	private TextView bottomTime;

	private LinearLayout leftDrawer;

	private MainMenuAdapter mainMenuAdapter;

	private ListView mainMenuListView;

	private LinearLayout rightDrawer;

	private ListView notationListView;

	private NotatinAdapter notationAdapter;

	private ListView notationBranchListView;

	private NotationBranchAdapter notationBranchAdapter;

	private TextView notationText;

	private View contextMenuParentView;

	private ViewPager infoPager;

	private InfoPagerAdapter infoPageAdepter;

	private EvalGraph evalGraphView;

	// private AdView barnerView; // Removed: AdMob dependency not available

	private MainPresenter presenter;

	private SNotation notation = new SNotation();

	private bool changeNotationFlag;


	private int defaultTimeTextColor;

	private bool keepScreenOn;

	private bool pieceSeFlag;

	private int selpvnum;

	// private AdRequest adrequest; // Removed: AdMob dependency not available

	// private MyInterstitialAd interstitial; // Removed: AdMob dependency not available

	private readonly MainMenuItem[] menuItems = new MainMenuItem[14]
	{
		new MainMenuItem(Resource.Id.game_start, Resource.String.Menu_NewGame_Text),
		new MainMenuItem(Resource.Id.game_continue, Resource.String.Menu_ContinuedGame_Text),
		new MainMenuItem(Resource.Id.game_stop, Resource.String.Menu_StopGame_Text),
		new MainMenuItem(Resource.Id.game_resign, Resource.String.Menu_ResignGame_Text),
		new MainMenuItem(Resource.Id.notation_analysis, Resource.String.Menu_Analysis_Text),
		new MainMenuItem(),
		new MainMenuItem(Resource.Id.menu_file, Resource.String.Menu_File_Text),
		new MainMenuItem(Resource.Id.menu_edit, Resource.String.MenuEdit_Text),
		new MainMenuItem(Resource.Id.menu_disp, Resource.String.MenuDisp_Text),
		new MainMenuItem(Resource.Id.menu_engine, Resource.String.Menu_EngineManage_Text),
		new MainMenuItem(Resource.Id.menu_vastai, Resource.String.Menu_VastAi_Text),
		new MainMenuItem(),
		new MainMenuItem(Resource.Id.action_settings, Resource.String.action_settings),
		new MainMenuItem(Resource.Id.menu_about, Resource.String.Menu_About_Text)
	};

	private const int REQUEST_WRITE_STORAGE = 0;

	private static readonly Dictionary<MainViewMessageId, int> MessageMap = new Dictionary<MainViewMessageId, int>
	{
		{
			MainViewMessageId.Initializing,
			Resource.String.MessageInitialized_Text
		},
		{
			MainViewMessageId.GameStart,
			Resource.String.MessageGameStart_Text
		},
		{
			MainViewMessageId.GameStop,
			Resource.String.MessageGameStop_Text
		},
		{
			MainViewMessageId.GameOver,
			Resource.String.MessageGameOver_Text
		},
		{
			MainViewMessageId.InitializeError,
			Resource.String.MessageInitializeError_Text
		},
		{
			MainViewMessageId.EngineTerminated,
			Resource.String.MessageEngineTerminated_Text
		}
	};

	private bool storagePermission;

	private void InitCommand()
	{
		commands.Add(CmdNo.Reverse, Resource.Id.cmd_reverse, Reverse, null);
		commands.Add(CmdNo.Kyokumen, Resource.Id.cmd_kyokumen, KyokumenPedia, null);
		commands.Add(CmdNo.InputCancel, Resource.Id.cmd_input_cancel, presenter.InputCancel, presenter.CanInputCancel);
		commands.Add(CmdNo.Pass, Resource.Id.cmd_pass, presenter.Pass, presenter.CanPass);
		commands.Add(CmdNo.CmmentEdit, Resource.Id.comment_edit, ShowCommentEditDialog, presenter.CanCommentEdit);
		commands.Add(CmdNo.CommentInfoSelect, Resource.Id.comment_info_select, ShowCommentInfoDialog, presenter.CanCommentEdit);
		commands.Add(CmdNo.CommentJointBoard, Resource.Id.comment_moves, ShowCommentJointBoard, presenter.CanCommentEdit);
		commands.Add(CmdNo.NotationCopy, Resource.Id.notation_copy, CopyToClipboard, null);
		commands.Add(CmdNo.NotationPaste, Resource.Id.notation_paste, PasteFromClipboard, presenter.CanPaste);
		commands.Add(CmdNo.Next, Resource.Id.cmd_next, presenter.Next, presenter.CanNext);
		commands.Add(CmdNo.Prev, Resource.Id.cmd_prev, presenter.Prev, presenter.CanNext);
		commands.Add(CmdNo.First, Resource.Id.cmd_first, presenter.First, presenter.CanMove);
		commands.Add(CmdNo.Last, Resource.Id.cmd_last, presenter.Last, presenter.CanMove);
		commands.Add(CmdNo.AutoPlay, Resource.Id.cmd_auto_play, presenter.AutoPlayStart, presenter.CanAutoPlay);
		commands.Add(CmdNo.JointBoard, Resource.Id.cmd_joint_board, Show2ndBoard, null);
		commands.Add(CmdNo.FileNew, Resource.Id.file_init, presenter.InitNotation, presenter.CanLoadNotaton);
		commands.Add(CmdNo.FileLoad, Resource.Id.file_load, FileLoad, presenter.CanLoadNotaton);
		commands.Add(CmdNo.FileLoadLast, Resource.Id.file_load_last, LoadLastGame, presenter.CanLoadNotaton);
		commands.Add(CmdNo.FileSave, Resource.Id.file_save, FileSave, CanSaveNotation);
		commands.Add(CmdNo.FileImport, Resource.Id.file_import, NotationSelectRequest, presenter.CanLoadNotaton);
		commands.Add(CmdNo.FileWebImport, Resource.Id.file_web_import, ShowWEBNoationDialog, presenter.CanLoadNotaton);
		commands.Add(CmdNo.FileSend, Resource.Id.file_send, FileSend, null);
		commands.Add(CmdNo.FileWebImportAs, Resource.Id.file_web_import_as, WebImportAs, presenter.CanLoadNotaton);
		commands.Add(CmdNo.OpenNotationSaveFolder, Resource.Id.file_open_folder, OpenNotationSaveFolder, presenter.CanLoadNotaton);
		commands.Add(CmdNo.GameStart, Resource.Id.game_start, GameStart, presenter.CanGameStart);
		commands.Add(CmdNo.GameContinue, Resource.Id.game_continue, GameContinue, presenter.CanGameStart);
		commands.Add(CmdNo.GameStop, Resource.Id.game_stop, presenter.Stop, null);
		commands.Add(CmdNo.GameResign, Resource.Id.game_resign, presenter.Resign, presenter.CanMakeMove);
		commands.Add(CmdNo.NotationAnalysis, Resource.Id.notation_analysis, ShowAnalyzeStartDialog, presenter.CanAnalyzerStart);
		commands.Add(CmdNo.Consider, Resource.Id.consider, ConsiderStart, presenter.CanConsiderStart);
		commands.Add(CmdNo.BoardEdit, Resource.Id.menu_board_edit, BoardEdit, presenter.CanEditBoard);
		commands.Add(CmdNo.CameraRead, Resource.Id.camera_read, CameraRead, presenter.CanEditBoard);
		commands.Add(CmdNo.MangeEgien, Resource.Id.menu_engine, PopupEngineMenu, presenter.CanManageEngine);
	}

	private void MenuGrayout(IMenu menu)
	{
		for (int i = 0; i < menu.Size(); i++)
		{
			IMenuItem item = menu.GetItem(i);
			item.SetEnabled(commands.IsEnable(item.ItemId));
		}
	}

	private void MainMenuGryout(MainMenuAdapter adapter)
	{
		foreach (MainMenuItem menuItem in adapter.GetMenuItems())
		{
			if (menuItem.TextId != 0)
			{
				menuItem.Enable = commands.IsEnable((int)menuItem.Id);
			}
		}
		adapter.UpdateGrayout();
	}

	private bool MenuExceute(int id)
	{
		if (!commands.IsEnable(id))
		{
			return false;
		}
		return commands.Execute(id);
	}

	private bool MenuExceute(CmdNo cmdno)
	{
		if (!commands.IsEnable(cmdno))
		{
			return false;
		}
		return commands.Execute(cmdno);
	}

	private int GetCommandId(CmdNo cmdno)
	{
		return commands.GetId(cmdno);
	}

	private void Reverse()
	{
		presenter.Reverse = !presenter.Reverse;
		UpdateReverse();
	}

	private void CopyToClipboard()
	{
		ClipboardManager obj = (ClipboardManager)GetSystemService("clipboard");
		ClipData primaryClip = ClipData.NewPlainText("kif text", presenter.NotaitonToString());
		obj.PrimaryClip = primaryClip;
	}

	private void PasteFromClipboard()
	{
		ClipData primaryClip = ((ClipboardManager)GetSystemService("clipboard")).PrimaryClip;
		if (primaryClip != null)
		{
			ClipData.Item itemAt = primaryClip.GetItemAt(0);
			presenter.PasteNotation(itemAt.Text);
			PopupNotationInfo();
		}
	}

	private void Show2ndBoard()
	{
		JointBoardDialog.NewInstance(presenter.Reverse, new SNotation(notation)).Show(FragmentManager, "JointBoardDialog");
	}

	private void FileLoad()
	{
		ShowOpenNotationDialog(LocalFile.KifPath);
	}

	private void LoadLastGame()
	{
		if (presenter.LoadTempNotation())
		{
			PopupNotationInfo();
		}
	}

	private void FileSave()
	{
		ShowSaveNotationDialog(LocalFile.KifPath, (presenter.NotationFileName != string.Empty) ? presenter.NotationFileName : presenter.NotationNewFileName, notation.BlackName, notation.WhiteName);
	}

	private bool CanSaveNotation()
	{
		return storagePermission;
	}

	private void FileSend()
	{
		if (presenter.NotationModified)
		{
			ShowSaveNotationDialog(LocalFile.KifPath, (presenter.NotationFileName != string.Empty) ? presenter.NotationFileName : presenter.NotationNewFileName, notation.BlackName, notation.WhiteName, delegate
			{
				NotationSendRequest();
			});
		}
		else
		{
			NotationSendRequest();
		}
	}

	private void GameStart()
	{
		presenter.Stop();
		ShowGameStartdialog();
	}

	private void GameContinue()
	{
		presenter.Stop();
		infoPager.SetCurrentItem(1, smoothScroll: false);
		presenter.GameStart(continued: true);
	}

	private void BoardEdit()
	{
		StartActivityForResult(new Intent(this, typeof(EditBoardActivity)), 103);
	}

	private void CameraRead()
	{
		StartActivityForResult(new Intent(this, typeof(KishinAnalyticsActivity)), CAMERA_READ_CODE);
	}

	private void KyokumenPedia()
	{
		string text = "http://kyokumen.jp/positions/";
		text += notation.Position.PositionToString(0).Replace(" ", "%20");
		ShowUrl(text);
	}

	private void ShogiWars()
	{
		if (Settings.AppSettings.WarsUserName == string.Empty)
		{
			ShowUserNameDialog();
		}
		if (Settings.AppSettings.WarsUserName != string.Empty)
		{
			ShowWarsGameResult();
		}
	}

	private void WebImportAs()
	{
		if (string.IsNullOrEmpty(presenter.WEBNotationUrl))
		{
			ShowWEBNoationDialog();
		}
		else if (presenter.LoadNotationFromWeb(presenter.WEBNotationUrl))
		{
			PopupNotationInfo();
		}
	}

	private void ConsiderStart()
	{
		presenter.Stop();
		infoPager.SetCurrentItem(1, smoothScroll: false);
		presenter.ConsiderStart();
	}

	protected override void OnCreate(Bundle bundle)
	{
		base.OnCreate(bundle);
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
		AppDebug.Log.Initialize();
		AppDebug.Log.Info("MainActivity.OnCreate started");
		RequestWindowFeature(WindowFeatures.NoTitle);
		Settings.Load();
		InitUI();
		PlaySE.Initialize(this);
		presenter = new MainPresenter(this);
		presenter.Initialize();
		InitCommand();

		// vast.ai watchdog: notify user when instance is auto-suspended
		VastAiWatchdog.Instance.InstanceAutoSuspended += (instanceId) =>
		{
			RunOnUiThread(() =>
			{
				Toast.MakeText(this, $"vast.ai インスタンス #{instanceId} をアイドルのため自動休止しました", ToastLength.Long).Show();
			});
		};
		UpdateSettings();
		UpdateNotation(NotationEventId.OBJECT_CHANGED);
		CreateFolders();
		if (Intent != null)
		{
			if (Intent.Type != null && Intent.Type == "message/rfc822")
			{
				IntentRecive(Intent);
			}
			else if (presenter.LoadNotationFromWeb(Intent.DataString))
			{
				PopupNotationInfo();
			}
		}
	}

	protected override void OnNewIntent(Intent intent)
	{
		base.OnNewIntent(intent);
		_ = Intent;
		if (intent != null && presenter.LoadNotationFromWeb(intent.DataString))
		{
			PopupNotationInfo();
		}
	}

	private void InitUI()
	{
		UpdateWindowSettings();
		SetContentView(Resource.Layout.maina);
		shogiBoard = FindViewById<ShogiBoard>(Resource.Id.shogiboard);
		shogiBoard.MakeMoveEvent += ShogiBoard_MakeMoveEvent;
		shogiBoard.AnimationEnd += ShogiBoard_AnimationEnd;

		// 形勢バーを盤面の左に追加
		evalBar = new EvalBar(this);
		var boardParent = (RelativeLayout)shogiBoard.Parent;
		var evalBarLp = new RelativeLayout.LayoutParams(
			(int)(16 * Resources.DisplayMetrics.Density),
			RelativeLayout.LayoutParams.MatchParent);
		evalBarLp.AddRule(LayoutRules.AlignParentLeft);
		evalBar.LayoutParameters = evalBarLp;
		boardParent.AddView(evalBar);
		evalBar.Visibility = ViewStates.Gone;
		prevButton = FindViewById<ImageButton>(Resource.Id.prev_button);
		prevButton.Click += PrevButton_Click;
		nextButton = FindViewById<ImageButton>(Resource.Id.next_button);
		nextButton.Click += NextButton_Click;
		firstButton = FindViewById<ImageButton>(Resource.Id.first_button);
		firstButton.Click += FirstButton_Click;
		lastButton = FindViewById<ImageButton>(Resource.Id.last_button);
		lastButton.Click += LastButton_Click;
		analyzButton = FindViewById<View>(Resource.Id.analyze_button);
		analyzButton.Click += AnalyzeButton_Click;
		analyzButton.LongClick += AnalyzButton_LongClick;
		analyzeText = FindViewById<TextView>(Resource.Id.analyze_text);
		menuButton = FindViewById<ImageButton>(Resource.Id.menu_button);
		menuButton.Click += MenuButton_Click;
		FindViewById<ImageButton>(Resource.Id.camera_read).Click += (s, e) => CameraRead();
		reverseButton = FindViewById<ImageButton>(Resource.Id.reverse_button);
		reverseButton.Click += ReverseButton_Click;
		inputCancelButton = FindViewById<ImageButton>(Resource.Id.input_cancel_button);
		inputCancelButton.Click += InputCancelButton_Click;
		if (Settings.AppSettings.ReverseButotn != 50)
		{
			reverseButton.SetImageResource(Resource.Drawable.ic_fn);
		}
		stateText = FindViewById<TextView>(Resource.Id.state_text);
		stateText.Click += NotationText_Click;
		drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
		drawerLayout.DrawerOpened += DrawerLayout_DrawerOpened;
		drawerLayout.DrawerSlide += DrawerLayout_DrawerSlide;
		drawerLayout.LayoutChange += DrawerLayout_LayoutChange;
		leftDrawer = FindViewById<LinearLayout>(Resource.Id.left_drawer);
		mainMenuAdapter = new MainMenuAdapter(this, menuItems);
		mainMenuListView = FindViewById<ListView>(Resource.Id.main_manu_lsist_view);
		mainMenuListView.Adapter = mainMenuAdapter;
		mainMenuListView.ItemClick += MainMenuListView_ItemClick;
		TextView textView = FindViewById<TextView>(Resource.Id.app_name);
		AssemblyName name = Assembly.GetExecutingAssembly().GetName();
		textView.Text = name.Name + " ver " + name.Version;
		topName = FindViewById<TextView>(Resource.Id.top_name);
		topTime = FindViewById<TextView>(Resource.Id.top_time);
		bottomName = FindViewById<TextView>(Resource.Id.bottom_name);
		bottomTime = FindViewById<TextView>(Resource.Id.bottom_time);
		defaultTimeTextColor = bottomTime.CurrentTextColor;
		rightDrawer = FindViewById<LinearLayout>(Resource.Id.right_drawer);
		if (notationListView != null)
		{
			UnregisterForContextMenu(notationListView);
			notationListView.Adapter = null;
		}
		notationAdapter = new NotatinAdapter(this);
		notationListView = FindViewById<ListView>(Resource.Id.notaiton_list_view);
		notationListView.Adapter = notationAdapter;
		notationListView.ItemClick += NotationListView_ItemClick;
		notationBranchAdapter = new NotationBranchAdapter(this);
		notationBranchListView = FindViewById<ListView>(Resource.Id.notaiton_branch_list_view);
		notationBranchListView.Adapter = notationBranchAdapter;
		notationBranchListView.ItemClick += NotationBranchListView_ItemClick;
		RegisterForContextMenu(notationListView);
		notationText = FindViewById<TextView>(Resource.Id.notaton_text);
		notationText.Click += NotationText_Click;
		evalGraphView = FindViewById<EvalGraph>(Resource.Id.eval_graph);
		if (evalGraphView != null)
		{
			evalGraphView.SelectPosition += InfoPageAdepter_SelectPosition;
		}
		int num = 0;
		if (infoPager != null)
		{
			num = infoPager.CurrentItem;
			infoPager.Adapter = null;
		}
		infoPager = FindViewById<ViewPager>(Resource.Id.viewpager);
		if (infoPageAdepter == null)
		{
			infoPageAdepter = new InfoPagerAdapter(this);
			infoPageAdepter.SelectPosition += InfoPageAdepter_SelectPosition;
			infoPageAdepter.ThinkListViewItemClick += InfoPageAdepter_ThinkListViewItemClick;
			infoPageAdepter.ThinkListViewItemLongClick += InfoPageAdepter_ThinkListViewItemLongClick;
			infoPageAdepter.CommentLongClick += InfoPageAdepter_CommentLongClick;
		}
		infoPageAdepter.DispEvalGraph = evalGraphView == null;
		infoPager.Adapter = infoPageAdepter;
		if (num != 0)
		{
			infoPager.SetCurrentItem(num, smoothScroll: false);
		}
		else
		{
			infoPager.SetCurrentItem(1, smoothScroll: false);
		}
		// Ad banner removed: AdMob dependency not available
		// if (barnerView != null)
		// {
		// 	barnerView.Destroy();
		// }
		// barnerView = FindViewById<AdView>(Resource.Id.adView);
		// if (barnerView != null)
		// {
		// 	barnerView.LoadAd(adrequest);
		// }
	}

	private void DrawerLayout_LayoutChange(object sender, View.LayoutChangeEventArgs e)
	{
		UpdatePlayerInfoPosition();
	}

	protected override void OnResume()
	{
		base.OnResume();
		// if (barnerView != null)
		// {
		// 	barnerView.Resume();
		// }
		PlaySE.Initialize(this);
		presenter.Resume();
	}

	protected override void OnPause()
	{
		base.OnPause();
		// if (barnerView != null)
		// {
		// 	barnerView.Pause();
		// }
		shogiBoard.AnimationStop();
		StoreSettings();
		presenter.Pause();
		PlaySE.Destory();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		// if (barnerView != null)
		// {
		// 	barnerView.Destroy();
		// }
		presenter.Destory();
		PlaySE.Destory();
	}

	public override void OnConfigurationChanged(Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		StoreSettings();
		InitUI();
		UpdateSettings();
		UpdateState();
		UpdateHitInfo();
		UpdateNotation(NotationEventId.OBJECT_CHANGED);
	}

	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);
		if (hasFocus)
		{
			UpdateWindowSettings();
			// if (!interstitial.IsLoaded && !interstitial.IsLoading)
			// {
			// 	interstitial.LoadAd(new AdRequest.Builder().Build());
			// }
		}
		else
		{
			presenter.AutoPlayStop();
		}
	}

	public override bool OnCreateOptionsMenu(IMenu menu)
	{
		return base.OnCreateOptionsMenu(menu);
	}

	public override bool OnPrepareOptionsMenu(IMenu menu)
	{
		if (drawerLayout.IsDrawerOpen(3) || drawerLayout.IsDrawerOpen(5))
		{
			drawerLayout.CloseDrawers();
		}
		else
		{
			drawerLayout.OpenDrawer(3);
		}
		return false;
	}

	public override bool OnOptionsItemSelected(IMenuItem item)
	{
		if (!MenuItemSelected(item.ItemId))
		{
			return base.OnOptionsItemSelected(item);
		}
		return true;
	}

	private void DrawerLayout_DrawerOpened(object sender, DrawerLayout.DrawerOpenedEventArgs e)
	{
		if (e.DrawerView == leftDrawer)
		{
			MainMenuGryout(mainMenuAdapter);
		}
		else if (e.DrawerView == rightDrawer)
		{
			notationListView.SetSelection(notation.MoveCurrent.Number);
		}
		presenter.AutoPlayStop();
	}

	private void DrawerLayout_DrawerSlide(object sender, DrawerLayout.DrawerSlideEventArgs e)
	{
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
	{
		switch (requestCode)
		{
		case 100:
			if (resultCode == Result.Ok && data.Data != null)
			{
				NotationSelectResult(data.Data);
			}
			break;
		case 102:
			Settings.Load();
			UpdateSettings();
			break;
		case 103:
			if (resultCode == Result.Ok)
			{
				drawerLayout.CloseDrawers();
			}
			break;
		case 104:
			if (resultCode == Result.Ok)
			{
				drawerLayout.CloseDrawers();
			}
			break;
		case 105:
			if (resultCode == Result.Ok)
			{
				presenter.SetExternalEnginePath(data.GetStringExtra("path"));
				drawerLayout.CloseDrawers();
			}
			break;
		case 107:
			_ = -1;
			break;
		case 108:
			RecieveWarsGameResult();
			break;
		case CAMERA_READ_CODE:
			if (resultCode == Result.Ok && data != null)
			{
				string sfen = data.GetStringExtra("sfen");
				if (!string.IsNullOrEmpty(sfen))
				{
					SNotation newNotation = new SNotation();
					Sfen.LoadNotation(newNotation, sfen);
					Domain.Game.NotationModel.EditBoard(newNotation);
					StartActivityForResult(new Intent(this, typeof(EditBoardActivity)), EDIT_BOARD_CODE);
				}
			}
			break;
		case 200:
			if ((int)Build.VERSION.SdkInt >= 30 && Android.OS.Environment.IsExternalStorageManager)
			{
				storagePermission = true;
				presenter.CreateFolders();
			}
			break;
		case VASTAI_ACTIVITY_CODE:
			if (resultCode == Result.Ok)
			{
				// VastAiActivity has saved RemoteHost/RemotePort/EngineNo to disk.
				// Force engine reconnect by terminating before loading new settings.
				presenter.ForceEngineReconnect();
				Settings.Load();
				drawerLayout.CloseDrawers();
			}
			break;
		case 101:
		case 106:
			break;
		}
	}

	public override void OnCreateContextMenu(IContextMenu menu, View v, IContextMenuContextMenuInfo menuInfo)
	{
		contextMenuParentView = v;
		if (v.Id != Resource.Id.notaiton_list_view)
		{
			return;
		}
		AdapterView.AdapterContextMenuInfo adapterContextMenuInfo = (AdapterView.AdapterContextMenuInfo)menuInfo;
		MoveNode moveNode = notation.GetMoveNode(adapterContextMenuInfo.Position);
		if (moveNode.Parent != null && moveNode.Parent.Children.Count > 1)
		{
			for (int i = 0; i < moveNode.Parent.Children.Count; i++)
			{
				MoveNode moveNode2 = moveNode.Parent.Children[i];
				string title = $"{moveNode2.Turn.ToChar()}{moveNode2.ToString(Settings.AppSettings.MoveStyle)}";
				menu.Add(0, i, i, title);
			}
		}
	}

	public override bool OnContextItemSelected(IMenuItem item)
	{
		if (contextMenuParentView != null && contextMenuParentView.Id == Resource.Id.notaiton_list_view)
		{
			AdapterView.AdapterContextMenuInfo adapterContextMenuInfo = (AdapterView.AdapterContextMenuInfo)item.MenuInfo;
			notation.GetMoveNode(adapterContextMenuInfo.Position);
			presenter.ChangeBranch(adapterContextMenuInfo.Position, item.ItemId);
			drawerLayout.CloseDrawers();
			return true;
		}
		return base.OnOptionsItemSelected(item);
	}

	private void ShogiBoard_MakeMoveEvent(object sender, MakeMoveEventArgs e)
	{
		presenter.AutoPlayStop();
		if (Settings.AppSettings.PlaySE)
		{
			PlaySE.Play(SeNo.KOMA);
		}
		presenter.MakeMove(e.MoveData);
	}

	private void ShogiBoard_AnimationEnd(object sender, EventArgs e)
	{
		if (pieceSeFlag)
		{
			pieceSeFlag = false;
			PlaySE.Play(SeNo.KOMA);
		}
		presenter.TakeTurn();
	}

	private void PrevButton_Click(object sender, EventArgs e)
	{
		presenter.Prev();
	}

	private void NextButton_Click(object sender, EventArgs e)
	{
		presenter.Next();
	}

	private void AnalyzeButton_Click(object sender, EventArgs e)
	{
		presenter.AutoPlayStop();
		infoPager.SetCurrentItem(1, smoothScroll: false);
		if (presenter.GameMode == GameMode.Consider)
		{
			presenter.Stop();
		}
		else
		{
			ConsiderStart();
		}
	}

	private void FirstButton_Click(object sender, EventArgs e)
	{
		presenter.First();
	}

	private void LastButton_Click(object sender, EventArgs e)
	{
		presenter.Last();
	}

	private void InputCancelButton_Click(object sender, EventArgs e)
	{
		presenter.InputCancel();
	}

	private void AnalyzButton_LongClick(object sender, View.LongClickEventArgs e)
	{
		presenter.EngineTerminate();
	}

	private void UpdateAnalyzeButton(bool analyzing)
	{
		if (analyzing == isAnalyzeActive) return;
		isAnalyzeActive = analyzing;
		if (analyzing)
		{
			analyzButton.SetBackgroundResource(Resource.Drawable.analyze_btn_bg_active);
			if (analyzeText != null) analyzeText.Text = "解析終了 00:00";
			analyzeStartTime = DateTime.Now;
			StartAnalyzeTimer();
		}
		else
		{
			analyzButton.SetBackgroundResource(Resource.Drawable.analyze_btn_bg_idle);
			if (analyzeText != null) analyzeText.Text = "解析開始";
			StopAnalyzeTimer();
		}
	}

	private void StartAnalyzeTimer()
	{
		StopAnalyzeTimer();
		analyzeTimer = new System.Timers.Timer(1000);
		analyzeTimer.Elapsed += (s, e) =>
		{
			RunOnUiThread(() =>
			{
				if (analyzeText != null && isAnalyzeActive)
				{
					var elapsed = DateTime.Now - analyzeStartTime;
					analyzeText.Text = $"解析終了 {(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
				}
			});
		};
		analyzeTimer.Start();
	}

	private void StopAnalyzeTimer()
	{
		if (analyzeTimer != null)
		{
			analyzeTimer.Stop();
			analyzeTimer.Dispose();
			analyzeTimer = null;
		}
	}

	private void MateButton_Click(object sender, EventArgs e)
	{
		presenter.Mate();
	}

	private void MenuButton_Click(object sender, EventArgs e)
	{
		if (presenter.AutoPlay)
		{
			presenter.AutoPlayStop();
		}
		else if (drawerLayout.IsDrawerOpen(3) || drawerLayout.IsDrawerOpen(5))
		{
			drawerLayout.CloseDrawers();
		}
		else
		{
			drawerLayout.OpenDrawer(3);
		}
	}

	private void ReverseButton_Click(object sender, EventArgs e)
	{
		if (Settings.AppSettings.ReverseButotn == 50)
		{
			Reverse();
		}
		else
		{
			MenuExceute((CmdNo)Settings.AppSettings.ReverseButotn);
		}
	}

	private void NotationText_Click(object sender, EventArgs e)
	{
		presenter.AutoPlayStop();
		if (drawerLayout != null)
		{
			changeNotationFlag = false;
			notationListView.InvalidateViews();
			notationBranchListView.InvalidateViews();
			drawerLayout.OpenDrawer(rightDrawer);
		}
	}

	private bool MenuItemSelected(int itemId)
	{
		bool flag = true;
		switch (itemId)
		{
		case Resource.Id.action_settings:
			Settings.Save();
			StartActivityForResult(new Intent(this, typeof(SettingActivity)), 102);
			drawerLayout.CloseDrawers();
			break;
		case Resource.Id.menu_file:
			PopupFileMenu();
			break;
		case Resource.Id.menu_edit:
			PopupEditMenu();
			break;
		case Resource.Id.menu_disp:
			PopupDispMenu();
			break;
		case Resource.Id.menu_engine:
			PopupEngineMenu();
			break;
		case Resource.Id.menu_vastai:
			StartActivityForResult(new Intent(this, typeof(VastAiActivity)), VASTAI_ACTIVITY_CODE);
			drawerLayout.CloseDrawers();
			break;
		case Resource.Id.engine_select:
			ShowEngineSelectDialog();
			break;
		case Resource.Id.engine_settings_wrapper:
			StartActivityForResult(new Intent(this, typeof(EngineSettingsWrapperActivity)), 110);
			break;
		case Resource.Id.engine_options:
			StartActivityForResult(new Intent(this, typeof(EngineOptionsActivity)), 104);
			break;
		case Resource.Id.engine_install:
			StartActivityForResult(new Intent(this, typeof(EngineInstallActivity)), 107);
			break;
		case Resource.Id.engine_folder:
			ShowSelectEngineFolderDialog();
			break;
		case Resource.Id.menu_about:
			ShowUrl("http://shogidroid.siganus.com");
			break;
		case Resource.Id.thinkinfo_add_branch:
			presenter.AddBranch(selpvnum, infoPageAdepter.DispMode);
			break;
		case Resource.Id.thinkinfo_add_comment:
			presenter.AddComment(selpvnum, infoPageAdepter.DispMode);
			break;
		default:
			flag = MenuExceute(itemId);
			if (flag)
			{
				drawerLayout.CloseDrawers();
			}
			break;
		}
		return flag;
	}

	private void MainMenuListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
	{
		MenuItemSelected((int)e.Id);
	}

	private void NotationListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
	{
		if (presenter.GameMode == GameMode.Input || presenter.GameMode == GameMode.Consider)
		{
			presenter.Jump((int)e.Id);
			drawerLayout.CloseDrawers();
		}
	}

	private void NotationBranchListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
	{
		if (presenter.GameMode == GameMode.Input || presenter.GameMode == GameMode.Consider)
		{
			presenter.NextChild((int)e.Id);
			drawerLayout.CloseDrawers();
		}
	}

	private void InfoPageAdepter_SelectPosition(object sender, GraphPositoinEventArgs e)
	{
		presenter.Jump(e.Number);
	}

	private void InfoPageAdepter_ThinkListViewItemLongClick(object sender, AdapterView.ItemLongClickEventArgs e)
	{
		presenter.AutoPlayStop();
		if ((presenter.GameMode == GameMode.Input || presenter.GameMode == GameMode.Consider) && presenter.CheckPvInfo(e.Position, infoPageAdepter.DispMode))
		{
			PopupThinkInfoMenu(e.Position);
		}
	}

	private void InfoPageAdepter_ThinkListViewItemClick(object sender, AdapterView.ItemClickEventArgs e)
	{
		presenter.AutoPlayStop();
		if (presenter.CheckPvInfo(e.Position, infoPageAdepter.DispMode))
		{
			SNotation sNotation = presenter.LoadPv(e.Position, infoPageAdepter.DispMode);
			if (sNotation != null)
			{
				JointBoardDialog.NewInstance(presenter.Reverse, sNotation).Show(FragmentManager, "JointBoardDialog");
			}
		}
	}

	private void InfoPageAdepter_CommentLongClick(object sender, View.LongClickEventArgs e)
	{
		presenter.AutoPlayStop();
		if (presenter.GameMode == GameMode.Input)
		{
			PopupCommentMenu();
		}
	}

	private void UpdateScreenOn()
	{
		bool flag = false;
		if (presenter.Busy || presenter.GameMode == GameMode.Play || presenter.ComState.IsThinking() || presenter.AutoPlay)
		{
			flag = true;
		}
		if (keepScreenOn != flag)
		{
			keepScreenOn = flag;
			if (flag)
			{
				Window.AddFlags(WindowManagerFlags.KeepScreenOn);
			}
			else
			{
				Window.ClearFlags(WindowManagerFlags.KeepScreenOn);
			}
		}
	}

	public void UpdateNotation(NotationEventId eventid)
	{
		if (eventid == NotationEventId.OBJECT_CHANGED)
		{
			notation = presenter.Notation;
			shogiBoard.Notation = notation;
			notationAdapter.SetNotation(notation);
			notationBranchAdapter.SetNotation(notation);
			infoPageAdepter.SetNotation(notation);
			if (evalGraphView != null)
			{
				evalGraphView.Notation = notation;
			}
		}
		shogiBoard.UpdateNotation(eventid);
		if (eventid == NotationEventId.LOAD || eventid == NotationEventId.INIT || eventid == NotationEventId.OBJECT_CHANGED)
		{
			SetName();
		}
		SetTime();
		UpdateNotationListView(eventid);
		UpdateNotationText(eventid);
		infoPageAdepter.Comment = notation.MoveCurrent.Comment;
		infoPageAdepter.UpdateNotation(eventid);
		if (evalGraphView != null)
		{
			evalGraphView.UpdateNotation(eventid);
		}
	}

	public void UpdateState()
	{
		shogiBoard.Busy = presenter.Busy;
		if (presenter.Busy)
		{
			stateText.Text = "busy...";
		}
		else
		{
			stateText.Text = ComStateToString(presenter.ComState);
		}
		UpdateAnalyzeButton(presenter.ComState == ComputerState.Analyzing || presenter.ComState == ComputerState.Mating);
		if (evalGraphView != null)
		{
			evalGraphView.DispComGraph = presenter.GameMode != GameMode.Play || Settings.AppSettings.ShowComputerThinking || presenter.BothComputer;
		}
		UpdateScreenOn();
	}

	public void UpdateInfo(PvInfos pvinfos)
	{
		if (presenter.GameMode != GameMode.Play || Settings.AppSettings.ShowComputerThinking || !presenter.IsEngineModePlay || presenter.BothComputer)
		{
			infoPageAdepter.SetThinkInfo(pvinfos);
		}
		if (presenter.ComState == ComputerState.Analyzing)
		{
			UpdateHitInfo();
		}
		UpdateEvalBar(pvinfos);
	}

	private void UpdateEvalBar(PvInfos pvinfos)
	{
		if (evalBar == null || pvinfos == null) return;

		// 最善手(rank 1)の評価値を取得
		if (pvinfos.ContainsKey(1))
		{
			var best = pvinfos[1];
			if (best.HasMate)
			{
				evalBar.SetEval(0, true, best.Mate);
			}
			else if (best.HasScore)
			{
				evalBar.SetEval(best.Score);
			}
			evalBar.Visibility = ViewStates.Visible;
		}
	}

	public void UpdateHitInfo()
	{
		shogiBoard.HintInfo = presenter.HintInfo;
	}

	public void SetPlayer(bool black, bool white)
	{
		shogiBoard.PlayerHumanBlack = black;
		shogiBoard.PlayerHumanWhite = white;
	}

	public void MessageError(string error)
	{
		MessageBox.ShowError(FragmentManager, error);
	}

	public void Message(MainViewMessageId id)
	{
		if (MessageMap.ContainsKey(id))
		{
			Toast.MakeText(this, MessageMap[id], ToastLength.Short).Show();
		}
	}

	public void MessagePopup(int resid)
	{
		Toast.MakeText(this, resid, ToastLength.Short).Show();
	}

	public void MessagePopup(string str, bool lengthShort = false)
	{
		Toast.MakeText(this, str, (!lengthShort) ? ToastLength.Long : ToastLength.Short).Show();
	}

	public void Moved(bool engine)
	{
		if (engine && Settings.AppSettings.PlaySE)
		{
			if (Settings.AppSettings.AnimationSpeed != AnimeSpeed.Off)
			{
				pieceSeFlag = true;
			}
			else
			{
				PlaySE.Play(SeNo.KOMA);
			}
		}
	}

	public void UpdateReverse()
	{
		shogiBoard.Reverse = presenter.Reverse;
		if (evalBar != null)
			evalBar.DispReverse = presenter.Reverse;
		SetName();
		SetTime();
	}

	public void UpdateTime()
	{
		SetTime();
	}

	public void AutoPlayState(bool play)
	{
		UpdateScreenOn();
		if (presenter.AutoPlay)
		{
			menuButton.SetImageResource(Resource.Drawable.ic_media_stop);
		}
		else
		{
			menuButton.SetImageResource(Resource.Drawable.ic_overflow);
		}
	}

	public void ShowInterstitial()
	{
		// Removed: AdMob dependency not available
		// if (interstitial.IsLoaded)
		// {
		// 	interstitial.Show(this);
		// }
	}

	private void IntentRecive(Intent intent)
	{
		string stringExtra = intent.GetStringExtra("android.intent.extra.SUBJECT");
		string stringExtra2 = intent.GetStringExtra("android.intent.extra.TEXT");
		if (presenter.LoadNotationFromString(stringExtra, stringExtra2))
		{
			PopupNotationInfo();
		}
	}

	private string ComStateToString(ComputerState state)
	{
		string result = string.Empty;
		switch (state)
		{
		case ComputerState.Analyzing:
			result = GetText(Resource.String.Analyzing_Text);
			break;
		case ComputerState.Thinking:
			result = GetText(Resource.String.Thinking_Text);
			break;
		case ComputerState.Ponder:
			result = GetText(Resource.String.Ponder_Text);
			break;
		case ComputerState.Mating:
			result = GetText(Resource.String.Mating_Text);
			break;
		}
		return result;
	}

	private void CreateFolders()
	{
		if ((int)Build.VERSION.SdkInt >= 30)
		{
			if (!Android.OS.Environment.IsExternalStorageManager)
			{
				RequestManageExternalStorage();
				return;
			}
		}
		else
		{
			if (ContextCompat.CheckSelfPermission(this, "android.permission.WRITE_EXTERNAL_STORAGE") != Permission.Granted)
			{
				RequestWriteExternalStoragePermittion();
				return;
			}
		}
		storagePermission = true;
		presenter.CreateFolders();
	}

	private void RequestManageExternalStorage()
	{
		try
		{
			var intent = new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
			StartActivityForResult(intent, 200);
		}
		catch
		{
			var intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings);
			intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
			StartActivityForResult(intent, 200);
		}
	}

	private void RequestWriteExternalStoragePermittion()
	{
		if (ActivityCompat.ShouldShowRequestPermissionRationale(this, "android.permission.WRITE_EXTERNAL_STORAGE"))
		{
			MessageBox messageBox = MessageBox.ShowConfirm(FragmentManager, Resource.String.MessageBoxTitleConfirm_Text, Resource.String.permission_external_storage, MessageBox.MBType.MB_OK);
			messageBox.OKClick = (EventHandler<DialogClickEventArgs>)Delegate.Combine(messageBox.OKClick, (EventHandler<DialogClickEventArgs>)delegate
			{
				ActivityCompat.RequestPermissions(this, new string[1] { "android.permission.WRITE_EXTERNAL_STORAGE" }, 0);
			});
		}
		else
		{
			ActivityCompat.RequestPermissions(this, new string[1] { "android.permission.WRITE_EXTERNAL_STORAGE" }, 0);
		}
	}

	public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
	{
		if (requestCode == 0)
		{
			if (grantResults.Length != 0 && grantResults[0] == Permission.Granted)
			{
				CreateFolders();
			}
		}
		else
		{
			base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		}
	}

	private void ShowOpenNotationDialog(string path)
	{
		OpenNotationDialog openDialog = OpenNotationDialog.NewInstance(path);
		OpenNotationDialog openNotationDialog = openDialog;
		openNotationDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(openNotationDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			if (presenter.LoadNotation(openDialog.FileName))
			{
				PopupNotationInfo();
			}
		});
		openDialog.Show(FragmentManager, "OpenNotationDialog");
	}

	private void ShowSaveNotationDialog(string path, string filename, string blackName, string whiteName, funcSaveNotationOkDelegate ok = null)
	{
		SaveNotationDialog saveDialog = SaveNotationDialog.NewInstance(path, filename, blackName, whiteName);
		SaveNotationDialog saveNotationDialog = saveDialog;
		saveNotationDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(saveNotationDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			if ((presenter.NotationFileName == string.Empty || presenter.NotationFileName != saveDialog.FileName) && LocalFile.FileExist(path, saveDialog.FileName))
			{
				MessageBox messageBox = MessageBox.ShowConfirm(FragmentManager, Resource.String.OverwriteConfirmTitle_Text, Resource.String.OverwriteConfirmMessage_Text, MessageBox.MBType.MB_OKCANCEL);
				messageBox.OKClick = (EventHandler<DialogClickEventArgs>)Delegate.Combine(messageBox.OKClick, (EventHandler<DialogClickEventArgs>)delegate
				{
					presenter.SaveNotation(path, saveDialog.FileName);
					if (ok != null)
					{
						ok(saveDialog.FileName);
					}
					else
					{
						MessagePopup(Resource.String.Saved_Text);
					}
				});
			}
			else
			{
				presenter.SaveNotation(path, saveDialog.FileName);
				if (ok != null)
				{
					ok(saveDialog.FileName);
				}
				else
				{
					MessagePopup(Resource.String.Saved_Text);
				}
			}
		});
		saveDialog.Show(FragmentManager, "SaveNotationDialog");
	}

	private void ShowGameStartdialog()
	{
		GameStartDialog gameStartDialog = GameStartDialog.NewInstance();
		gameStartDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(gameStartDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			infoPager.SetCurrentItem(1, smoothScroll: false);
			presenter.GameStart(continued: false);
		});
		gameStartDialog.Show(FragmentManager, "GameStartDialog");
	}

	private void NotationSelectRequest()
	{
		Intent intent = new Intent("android.intent.action.GET_CONTENT");
		intent.SetType("*/*");
		try
		{
			StartActivityForResult(intent, 100);
		}
		catch (ActivityNotFoundException)
		{
			MessagePopup(Resource.String.ActivityNotFound_Text);
		}
	}

	private void NotationSelectResult(Android.Net.Uri uri)
	{
		if (uri.Scheme == "file")
		{
			string path = Util.GetPath(this, uri);
			if (presenter.LoadNotation(path))
			{
				PopupNotationInfo();
			}
			return;
		}
		string text = LoadTextFile(uri);
		if (!string.IsNullOrEmpty(text))
		{
			presenter.PasteNotation(text);
			PopupNotationInfo();
		}
	}

	private void NotationSendRequest()
	{
		Intent intent = new Intent("android.intent.action.SEND");
		intent.SetType("text/plane");
		using Java.IO.File file = new Java.IO.File(LocalFile.KifPath, presenter.NotationFileName);
		Android.Net.Uri value = ((Build.VERSION.SdkInt >= BuildVersionCodes.N) ? FileProvider.GetUriForFile(ApplicationContext, ApplicationContext.PackageName + ".provider", file) : Android.Net.Uri.FromFile(file));
		intent.PutExtra("android.intent.extra.STREAM", value);
		try
		{
			StartActivityForResult(intent, 101);
		}
		catch (ActivityNotFoundException)
		{
			MessagePopup(Resource.String.ActivityNotFound_Text);
		}
	}

	private void OpenNotationSaveFolder()
	{
		try
		{
			Intent intent = new Intent("android.intent.action.VIEW");
			Android.Net.Uri data;
			using (Java.IO.File file = new Java.IO.File(LocalFile.KifPath + Java.IO.File.Separator))
			{
				if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
				{
					MessagePopup(Resource.String.ActivityNotFound_Text);
					return;
				}
				data = Android.Net.Uri.FromFile(file);
			}
			intent.SetDataAndType(data, "resource/folder");
			intent.PutExtra("org.openintents.extra.ABSOLUTE_PATH", LocalFile.KifPath + Java.IO.File.Separator);
			try
			{
				StartActivity(intent);
			}
			catch (ActivityNotFoundException)
			{
				MessagePopup(Resource.String.ActivityNotFound_Text);
			}
		}
		catch
		{
		}
	}

	private void ShowWEBNoationDialog()
	{
		WEBNotationDialog dialog = WEBNotationDialog.NewInstance();
		string text = ClipboardUtil.GetData(this);
		if (string.IsNullOrEmpty(text))
		{
			text = presenter.WEBNotationUrl;
		}
		dialog.Url = text;
		WEBNotationDialog wEBNotationDialog = dialog;
		wEBNotationDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(wEBNotationDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			if (presenter.LoadNotationFromWeb(dialog.Url))
			{
				PopupNotationInfo();
			}
		});
		dialog.Show(FragmentManager, "WEBNotationDialog");
	}

	private void ShowUrl(string url)
	{
		Android.Net.Uri uri = Android.Net.Uri.Parse(url);
		StartActivity(new Intent("android.intent.action.VIEW", uri));
	}

	private void ShowWarsGameResult()
	{
		string text = "https://shogiwars.heroz.jp/games/history?user_id=" + Settings.AppSettings.WarsUserName;
		if (Locale.Default.Language != "ja")
		{
			text += "&locale=en";
		}
		Settings.AppSettings.ImportUrl = ClipboardUtil.GetData(this);
		Android.Net.Uri uri = Android.Net.Uri.Parse(text);
		StartActivityForResult(new Intent("android.intent.action.VIEW", uri), 108);
	}

	private void RecieveWarsGameResult()
	{
		string data = ClipboardUtil.GetData(this);
		if (!string.IsNullOrEmpty(data) && data != Settings.AppSettings.ImportUrl)
		{
			ShowWEBNoationDialog();
		}
	}

	private void ShowUserNameDialog()
	{
		UserNameDialog dialog = UserNameDialog.NewInstance();
		dialog.UserName = Settings.AppSettings.WarsUserName;
		UserNameDialog userNameDialog = dialog;
		userNameDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(userNameDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			Settings.AppSettings.WarsUserName = dialog.UserName;
			ShowWarsGameResult();
		});
		dialog.Show(FragmentManager, "UserNameDialog");
	}

	private void PopupFileMenu()
	{
		PopupMenu popupMenu = new PopupMenu(this, bottomName);
		popupMenu.Inflate(Resource.Menu.file_menu);
		MenuGrayout(popupMenu.Menu);
		popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs e)
		{
			MenuItemSelected(e.Item.ItemId);
		};
		popupMenu.DismissEvent += delegate
		{
			drawerLayout.CloseDrawers();
		};
		popupMenu.Show();
	}

	private void PopupEditMenu()
	{
		PopupMenu popupMenu = new PopupMenu(this, bottomName);
		popupMenu.Inflate(Resource.Menu.edit_menu);
		MenuGrayout(popupMenu.Menu);
		popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs e)
		{
			MenuItemSelected(e.Item.ItemId);
		};
		popupMenu.DismissEvent += delegate
		{
			drawerLayout.CloseDrawers();
		};
		popupMenu.Show();
	}

	private void PopupDispMenu()
	{
		PopupMenu popupMenu = new PopupMenu(this, bottomName);
		popupMenu.Inflate(Resource.Menu.disp_menu);
		MenuGrayout(popupMenu.Menu);
		popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs e)
		{
			MenuItemSelected(e.Item.ItemId);
		};
		popupMenu.DismissEvent += delegate
		{
			drawerLayout.CloseDrawers();
		};
		popupMenu.Show();
	}

	private void PopupEngineMenu()
	{
		View anchor = ((Resources.Configuration.Orientation == Orientation.Landscape) ? ((View)bottomName) : ((View)analyzButton));
		PopupMenu popupMenu = new PopupMenu(this, anchor);
		popupMenu.Inflate(Resource.Menu.engine_menu);
		presenter.AnalyzeStop();
		popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs e)
		{
			MenuItemSelected(e.Item.ItemId);
		};
		popupMenu.DismissEvent += delegate
		{
			drawerLayout.CloseDrawers();
		};
		popupMenu.Show();
	}

	private const int VASTAI_ACTIVITY_CODE = 120;

	private void ShowEngineSelectDialog()
	{
		EngineSelectDialog selectDialog = EngineSelectDialog.NewInstance(Settings.EngineSettings.GetExternalEngineFolder(), Settings.EngineSettings.EngineNo, Settings.EngineSettings.EngineName);
		EngineSelectDialog engineSelectDialog = selectDialog;
		engineSelectDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(engineSelectDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			presenter.SelectEngine(selectDialog.EngineNo, selectDialog.EngineName);
		});
		selectDialog.Show(FragmentManager, "EngineSelectDialog");
	}

	private void ShowSelectEngineFolderDialog()
	{
		Intent intent = new Intent(this, typeof(SelectFolderActivity));
		intent.PutExtra("path", Settings.EngineSettings.EngineFolder);
		intent.PutExtra("default", LocalFile.EnginePath);
		StartActivityForResult(intent, 105);
	}

	private void PopupThinkInfoMenu(int pvnum)
	{
		PopupMenu popupMenu = new PopupMenu(this, infoPager);
		popupMenu.Inflate(Resource.Menu.thnkinfomenu);
		selpvnum = pvnum;
		popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs e)
		{
			MenuItemSelected(e.Item.ItemId);
		};
		popupMenu.Show();
	}

	private void PopupCommentMenu()
	{
		PopupMenu popupMenu = new PopupMenu(this, infoPager);
		popupMenu.Inflate(Resource.Menu.comment_menu);
		popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs e)
		{
			MenuItemSelected(e.Item.ItemId);
		};
		popupMenu.Show();
	}

	private void ShowCommentEditDialog()
	{
		CommentEditDialog dialog = CommentEditDialog.NewInstance(notation.MoveCurrent.Comment);
		dialog.Show(FragmentManager, "CommentEditDialog");
		CommentEditDialog commentEditDialog = dialog;
		commentEditDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(commentEditDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			presenter.SetComment(dialog.Comment);
		});
	}

	private void ShowCommentInfoDialog()
	{
		ThinkListDialog thinkListDialog = ThinkListDialog.NewInstance(this, notation.MoveCurrent.CommentList, Settings.AppSettings.MoveStyle);
		thinkListDialog.Show(FragmentManager, "ThinkListDialog");
		thinkListDialog.ItemClick = (EventHandler<ThinkListDialogItemClickEventArgs>)Delegate.Combine(thinkListDialog.ItemClick, (EventHandler<ThinkListDialogItemClickEventArgs>)delegate(object sender, ThinkListDialogItemClickEventArgs e)
		{
			SNotation sNotation = presenter.LoadMoves(e.Moves);
			if (sNotation != null)
			{
				JointBoardDialog.NewInstance(presenter.Reverse, sNotation).Show(FragmentManager, "JointBoardDialog");
			}
		});
	}

	private void ShowCommentJointBoard()
	{
		if (notation.MoveCurrent.ThinkInfoCount == 1)
		{
			List<PvInfo> list = PvInfos.LoadPvInfos(notation.MoveCurrent.CommentList);
			if (list.Count != 0)
			{
				SNotation sNotation = presenter.LoadMoves(list[0].Message);
				if (sNotation != null)
				{
					JointBoardDialog.NewInstance(presenter.Reverse, sNotation).Show(FragmentManager, "JointBoardDialog");
				}
			}
		}
		else
		{
			ShowCommentInfoDialog();
		}
	}

	private void ShowAnalyzeStartDialog()
	{
		AnalyzeStartDialog analyzeStartDialog = AnalyzeStartDialog.NewInstance();
		presenter.Stop();
		analyzeStartDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(analyzeStartDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			presenter.AnalyzerStart();
		});
		analyzeStartDialog.Show(FragmentManager, "AnalyzeStartDialog");
	}

	private void CreateShortcutMenu(IMenu menu)
	{
		int[] obj = new int[6]
		{
			Settings.AppSettings.ShortcutMenu1,
			Settings.AppSettings.ShortcutMenu2,
			Settings.AppSettings.ShortcutMenu3,
			Settings.AppSettings.ShortcutMenu4,
			Settings.AppSettings.ShortcutMenu5,
			Settings.AppSettings.ShortcutMenu6
		};
		string[] stringArray = Resources.GetStringArray(Resource.Array.SettingsShortcutMenu_Texts);
		string[] stringArray2 = Resources.GetStringArray(Resource.Array.SettingsShortcutMenu_Values);
		menu.RemoveGroup(0);
		int[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			int no = array[i];
			if (no != 0)
			{
				int num = Array.FindIndex(stringArray2, (string x) => x == no.ToString());
				if (num >= 0)
				{
					int commandId = GetCommandId((CmdNo)no);
					menu.Add(0, commandId, 0, stringArray[num]);
				}
			}
		}
	}

	private void PopupShortcutMenu()
	{
		PopupMenu popupMenu = new PopupMenu(this, menuButton);
		CreateShortcutMenu(popupMenu.Menu);
		popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs e)
		{
			MenuItemSelected(e.Item.ItemId);
		};
		popupMenu.Show();
	}

	private void SetName()
	{
		if (presenter.Reverse)
		{
			topName.Text = notation.BlackName;
			bottomName.Text = notation.WhiteName;
		}
		else
		{
			bottomName.Text = notation.BlackName;
			topName.Text = notation.WhiteName;
		}
	}

	private void SetTime()
	{
		if (presenter.GameMode == GameMode.Play)
		{
			var blackRT = presenter.BlackRemainTime;
			var whiteRT = presenter.WhiteRemainTime;
			bool isTimed = blackRT.HaveTime > 0 || blackRT.HaveByoyomi > 0 || blackRT.HaveIncrement > 0
			             || whiteRT.HaveTime > 0 || whiteRT.HaveByoyomi > 0 || whiteRT.HaveIncrement > 0;
			if (isTimed)
			{
				if (presenter.Reverse)
				{
					topTime.Text = GetRemainTimeString(blackRT);
					bottomTime.Text = GetRemainTimeString(whiteRT);
				}
				else
				{
					bottomTime.Text = GetRemainTimeString(blackRT);
					topTime.Text = GetRemainTimeString(whiteRT);
				}
			}
			else
			{
				if (presenter.Reverse)
				{
					topTime.Text = GetTimeString(presenter.BlackTime);
					bottomTime.Text = GetTimeString(presenter.WhiteTime);
				}
				else
				{
					bottomTime.Text = GetTimeString(presenter.BlackTime);
					topTime.Text = GetTimeString(presenter.WhiteTime);
				}
			}
		}
		else if (notation.MoveCurrent.Number == 0)
		{
			topTime.Text = MoveTotalTimeString(notation.MoveCurrent);
			bottomTime.Text = MoveTotalTimeString(notation.MoveCurrent);
		}
		else
		{
			MoveNode move;
			MoveNode move2;
			if (notation.MoveCurrent.Turn == PlayerColor.Black)
			{
				move = notation.MoveCurrent;
				move2 = notation.MovePrev;
			}
			else
			{
				move2 = notation.MoveCurrent;
				move = notation.MovePrev;
			}
			if (presenter.Reverse)
			{
				topTime.Text = MoveTotalTimeString(move);
				bottomTime.Text = MoveTotalTimeString(move2);
			}
			else
			{
				bottomTime.Text = MoveTotalTimeString(move);
				topTime.Text = MoveTotalTimeString(move2);
			}
		}
		PlayerColor playerColor = PlayerColor.Black;
		if (shogiBoard.Reverse)
		{
			playerColor = PlayerColor.White;
		}
		if (notation.Position.Turn == playerColor)
		{
			bottomTime.SetTextColor(new Color(ContextCompat.GetColor(this, Resource.Color.time_text_color)));
			topTime.SetTextColor(new Color(defaultTimeTextColor));
		}
		else
		{
			topTime.SetTextColor(new Color(ContextCompat.GetColor(this, Resource.Color.time_text_color)));
			bottomTime.SetTextColor(new Color(defaultTimeTextColor));
		}
	}

	public static string MoveTotalTimeString(MoveDataEx move)
	{
		_ = string.Empty;
		int num = move.TotalTime / 3600;
		int num2 = move.TotalTime - num * 3600;
		return $"{num:00}:{num2 / 60:00}:{num2 % 60:00}";
	}

	private string GetTimeString(int est_time)
	{
		int num = est_time / 1000;
		int num2 = num / 3600;
		int num3 = num - num2 * 3600;
		return $"{num2,3:0}:{num3 / 60:00}:{num3 % 60:00}";
	}

	private string GetRemainTimeString(ShogiGUI.Engine.GameRemainTime rt)
	{
		if (rt.Time > 0)
		{
			int totalSec = rt.Time / 1000;
			int h = totalSec / 3600;
			int rest = totalSec - h * 3600;
			return $"{h,3:0}:{rest / 60:00}:{rest % 60:00}";
		}
		if (rt.Byoyomi > 0)
		{
			int sec = rt.Byoyomi / 1000;
			return $"  {sec}秒";
		}
		return "  0:00:00";
	}

	private void UpdateNotationListView(NotationEventId eventid)
	{
		if ((uint)(eventid - 1) <= 1u || eventid == NotationEventId.COMMENT)
		{
			changeNotationFlag = true;
			notationListView.SetSelection(notation.MoveCurrent.Number);
		}
		else
		{
			notationAdapter.NotifyDataSetChanged();
			notationBranchAdapter.NotifyDataSetChanged();
			notationListView.SetSelection(notation.MoveCurrent.Number);
		}
	}

	private void UpdateNotationText(NotationEventId eventid)
	{
		if (notation.MoveCurrent.Number == 0)
		{
			notationText.Text = MoveStringExtention.InitialPosition(Settings.AppSettings.MoveStyle);
			return;
		}
		char c = notation.MoveCurrent.Turn.ToChar();
		notationText.Text = $"{notation.MoveCurrent.Number,3} {c}{notation.MoveCurrent.ToString(Settings.AppSettings.MoveStyle)}";
	}

	private void UpdatePlayerInfoPosition()
	{
		if (bottomTime.Width > shogiBoard.InfoWidth)
		{
			topName.SetTextSize(ComplexUnitType.Sp, 9f);
			topTime.SetTextSize(ComplexUnitType.Sp, 9f);
			bottomName.SetTextSize(ComplexUnitType.Sp, 9f);
			bottomTime.SetTextSize(ComplexUnitType.Sp, 9f);
		}
		int num = topName.Height + topTime.Height;
		int num2 = 0;
		if (num > shogiBoard.InfoHeight)
		{
			num2 = num - shogiBoard.InfoHeight;
			if (shogiBoard.TopInfoY < num2)
			{
				num2 = shogiBoard.TopInfoY;
			}
		}
		topName.TranslationX = shogiBoard.TopInfoX;
		topName.TranslationY = shogiBoard.TopInfoY - num2;
		topTime.TranslationX = shogiBoard.TopInfoX;
		topTime.TranslationY = shogiBoard.TopInfoY + topName.Height - num2;
		bottomTime.TranslationX = shogiBoard.BottomInfoX;
		bottomTime.TranslationY = shogiBoard.BottomInfoY - num2;
		bottomName.TranslationX = shogiBoard.BottomInfoX;
		bottomName.TranslationY = shogiBoard.BottomInfoY + bottomTime.Height - num2;
	}

	private void PopupNotationInfo()
	{
		string text = string.Empty;
		if (notation.KifuInfos.Contains("棋戦"))
		{
			text += notation.KifuInfos["棋戦"];
		}
		if (notation.BlackName != string.Empty || notation.WhiteName != string.Empty)
		{
			text = text + " " + notation.BlackName + " ";
			text += GetString(Resource.String.VS_Text);
			text = text + " " + notation.WhiteName;
		}
		int count = notation.Count;
		if (count > 0)
		{
			text = text + " " + count + GetString(Resource.String.NumMoves_Text);
		}
		if (text != string.Empty)
		{
			MessagePopup(text);
		}
	}

	private void UpdateSettings()
	{
		presenter.UpdateSettings();
		shogiBoard.AnimaSpeed = Settings.AppSettings.AnimationSpeed;
		shogiBoard.HintDisp = Settings.AppSettings.ShowHintArrow;
		shogiBoard.NextMoveDisp = Settings.AppSettings.ShowNextArrow;
		shogiBoard.MoveStyle = Settings.AppSettings.MoveStyle;
		shogiBoard.Reverse = presenter.Reverse;
		UpdateNotationText(NotationEventId.OTHER);
		notationAdapter.MoveStyle = Settings.AppSettings.MoveStyle;
		notationBranchAdapter.MoveStyle = Settings.AppSettings.MoveStyle;
		infoPageAdepter.MoveStyle = Settings.AppSettings.MoveStyle;
		infoPageAdepter.EvalGraphScaleFactor = Settings.AppSettings.EvalGraphScaleFactor;
		infoPageAdepter.EvalGraphLiner = Settings.AppSettings.GraphLiner;
		infoPageAdepter.PVDispaly = (PVDispMode)Settings.AppSettings.PVDisplay;
		if (evalGraphView != null)
		{
			evalGraphView.ScaleFactor = Settings.AppSettings.EvalGraphScaleFactor;
			evalGraphView.GraphLiner = Settings.AppSettings.GraphLiner;
		}
		if (Settings.AppSettings.ReverseButotn != 50)
		{
			reverseButton.SetImageResource(Resource.Drawable.ic_fn);
		}
		else
		{
			reverseButton.SetImageResource(17301581);
		}
		if (presenter.AutoPlay)
		{
			menuButton.SetImageResource(Resource.Drawable.ic_media_stop);
		}
		else
		{
			menuButton.SetImageResource(Resource.Drawable.ic_overflow);
		}
		UpdateWindowSettings();
		UpdateState();
	}

	private void StoreSettings()
	{
		if (evalGraphView != null)
		{
			Settings.AppSettings.EvalGraphScaleFactor = evalGraphView.ScaleFactor;
		}
		else
		{
			Settings.AppSettings.EvalGraphScaleFactor = infoPageAdepter.EvalGraphScaleFactor;
		}
	}

	private void UpdateWindowSettings()
	{
		if (Settings.AppSettings.DispToolbar)
		{
			// Show system bars (status bar + navigation bar)
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.Visible;
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
		}
		else
		{
			// Hide system bars (immersive sticky mode)
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
				SystemUiFlags.ImmersiveSticky |
				SystemUiFlags.HideNavigation |
				SystemUiFlags.Fullscreen);
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
		}
		AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, Settings.AppSettings.DispToolbar);
	}

	private string LoadTextFile(Android.Net.Uri uri)
	{
		string result = string.Empty;
		try
		{
			using Stream stream = ContentResolver.OpenInputStream(uri);
			result = StringUtil.Load(stream);
		}
		catch (Exception ex)
		{
			MessageError(ex.Message);
			result = string.Empty;
		}
		return result;
	}
}
