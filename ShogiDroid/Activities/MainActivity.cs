using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

[Activity(Label = "ShogiDroid", MainLauncher = true, LaunchMode = Android.Content.PM.LaunchMode.SingleTask, Icon = "@drawable/shogidroid_icon", WindowSoftInputMode = SoftInput.AdjustNothing, ConfigurationChanges = (ConfigChanges.Orientation | ConfigChanges.ScreenSize), Theme = "@style/AppTheme")]
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
[IntentFilter(new string[] { "android.intent.action.VIEW" }, Categories = new string[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" }, DataScheme = "https", DataHost = "kishin-analytics.heroz.jp")]
[IntentFilter(new string[] { "android.intent.action.SEND" }, Categories = new string[] { "android.intent.category.DEFAULT" }, DataMimeType = "message/rfc822")]
public class MainActivity : ThemedActivity, IMainView, ActivityCompat.IOnRequestPermissionsResultCallback, IJointBoardBranchHost, IJavaObject, IDisposable, IJavaPeerable
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

	private const int BACKGROUND_ANALYSIS_NOTIFICATION_ID = 4203;

	private const string BACKGROUND_ANALYSIS_NOTIFICATION_CHANNEL_ID = "background_analysis_status";

	private ShogiBoard shogiBoard;

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

	private Button bookBrowseCloseButton;

	private View topShortcutBar;

	private View topShortcutButtons;

	private ImageButton passButton;

	private ImageButton inputCancelButton;

	private TextView stateText;

	private DrawerLayout drawerLayout;

	private TextView topName;

	private TextView topTime;

	private TextView bottomName;

	private TextView bottomTime;

	private LinearLayout leftDrawer;

	private DrawerSectionAdapter drawerAdapter_;

	private ExpandableListView drawerListView_;

	private LinearLayout rightDrawer;

	private ListView notationListView;

	private NotationAdapter notationAdapter;

	private ListView notationBranchListView;

	private NotationBranchAdapter notationBranchAdapter;

	private TextView notationText;

	private TextView threatmateBadge;

	private bool isActivityVisible_;

	private CancellationTokenSource vastAiBootCts_;

	private Task vastAiBootTask_;

	private bool isDestroyed_;

	private View contextMenuParentView;

	private ViewPager infoPager;

	private InfoPagerAdapter infoPageAdepter;

	private EvalGraph evalGraphView;

	private ShogiGUI.Engine.RemoteMonitor remoteMonitor_;

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

	private List<DrawerSectionModel> BuildDrawerSections()
	{
		var sections = new List<DrawerSectionModel>();
		Func<int, Func<bool>> enabled = id => () => CanOpenDrawerItem(id);

		// 1. クイック操作（常時展開）
		var quick = new DrawerSectionModel("クイック操作", isQuickAction: true);
		quick.Add(Resource.Id.notation_analysis, GetString(Resource.String.Menu_Analysis_Text), isEnabled: enabled(Resource.Id.notation_analysis));
		quick.Add(Resource.Id.consider, GetString(Resource.String.Consider_Text), isEnabled: enabled(Resource.Id.consider));
		quick.Add(Resource.Id.camera_read, GetString(Resource.String.Menu_CameraRead_Text), isEnabled: enabled(Resource.Id.camera_read));
		quick.Add(Resource.Id.book_browse, GetString(Resource.String.Menu_BookBrowse_Text), isEnabled: enabled(Resource.Id.book_browse));
		quick.Add(Resource.Id.engine_select, GetString(Resource.String.Menu_EngineSelect_Text), isEnabled: enabled(Resource.Id.engine_select));
		quick.Add(Resource.Id.menu_vastai, GetString(Resource.String.Menu_VastAi_Text), isEnabled: enabled(Resource.Id.menu_vastai));
		sections.Add(quick);

		// 2. 解析
		var analyze = new DrawerSectionModel("解析");
		analyze.Add(Resource.Id.analysis_settings, GetString(Resource.String.Menu_AnalysisSettings_Text), isEnabled: enabled(Resource.Id.analysis_settings));
		analyze.Add(Resource.Id.display_settings, GetString(Resource.String.Menu_DisplaySettings_Text), isEnabled: enabled(Resource.Id.display_settings));
		analyze.Add(Resource.Id.notation_analysis, GetString(Resource.String.Menu_Analysis_Text), isEnabled: enabled(Resource.Id.notation_analysis));
		analyze.Add(Resource.Id.consider, GetString(Resource.String.Consider_Text), isEnabled: enabled(Resource.Id.consider));
		sections.Add(analyze);

		// 3. 棋譜
		var kifu = new DrawerSectionModel("棋譜");
		kifu.Add(Resource.Id.file_save, GetString(Resource.String.Menu_FileSave_Text), isEnabled: enabled(Resource.Id.file_save));
		kifu.Add(Resource.Id.file_save_overwrite, GetString(Resource.String.Menu_FileSaveOverwrite_Text), isEnabled: enabled(Resource.Id.file_save_overwrite));
		kifu.Add(Resource.Id.notation_paste, GetString(Resource.String.Menu_NotaitonPaste_Text), isEnabled: enabled(Resource.Id.notation_paste));
		kifu.Add(Resource.Id.file_open_folder, GetString(Resource.String.Menu_OpenKifuFolder_Text), isEnabled: enabled(Resource.Id.file_open_folder));
		kifu.Add(Resource.Id.file_load, GetString(Resource.String.Menu_FileLoad_Text), isEnabled: enabled(Resource.Id.file_load));
		kifu.Add(Resource.Id.file_web_import, GetString(Resource.String.Menu_FileWebExport_Text), isEnabled: enabled(Resource.Id.file_web_import));
		kifu.Add(Resource.Id.notation_copy, GetString(Resource.String.Menu_NotaitonCopy_Text), isEnabled: enabled(Resource.Id.notation_copy));
		sections.Add(kifu);

		// 4. 定跡
		var book = new DrawerSectionModel("定跡");
		book.Add(Resource.Id.book_browse, GetString(Resource.String.Menu_BookBrowse_Text), isEnabled: enabled(Resource.Id.book_browse));
		book.Add(Resource.Id.book_load, GetString(Resource.String.Menu_BookLoad_Text), isEnabled: enabled(Resource.Id.book_load));
		sections.Add(book);

		// 5. 局面
		var board = new DrawerSectionModel("局面");
		board.Add(Resource.Id.position_load, GetString(Resource.String.Menu_PositionLoad_Text), isEnabled: enabled(Resource.Id.position_load));
		board.Add(Resource.Id.camera_read, GetString(Resource.String.Menu_CameraRead_Text), isEnabled: enabled(Resource.Id.camera_read));
		board.Add(Resource.Id.cmd_reverse, GetString(Resource.String.MenuReverse_Text), isEnabled: enabled(Resource.Id.cmd_reverse));
		board.Add(Resource.Id.menu_board_edit, GetString(Resource.String.Menu_EditBoard_Text), isEnabled: enabled(Resource.Id.menu_board_edit));
		sections.Add(board);

		// 6. エンジン
		var engine = new DrawerSectionModel("エンジン");
		engine.Add(Resource.Id.engine_select, GetString(Resource.String.Menu_EngineSelect_Text), isEnabled: enabled(Resource.Id.engine_select));
		engine.Add(Resource.Id.engine_settings_wrapper, GetString(Resource.String.Menu_EngineSettings_Text), isEnabled: enabled(Resource.Id.engine_settings_wrapper));
		engine.Add(Resource.Id.engine_options, GetString(Resource.String.EngineSettingsAllOptions_Text), isEnabled: enabled(Resource.Id.engine_options));
		sections.Add(engine);

		// 7. 対局
		var game = new DrawerSectionModel("対局");
		game.Add(Resource.Id.game_start, GetString(Resource.String.Menu_NewGame_Text), isEnabled: enabled(Resource.Id.game_start));
		game.Add(Resource.Id.game_continue, GetString(Resource.String.Menu_ContinuedGame_Text), isEnabled: enabled(Resource.Id.game_continue));
		game.Add(Resource.Id.game_stop, GetString(Resource.String.Menu_StopGame_Text), isEnabled: enabled(Resource.Id.game_stop));
		game.Add(Resource.Id.game_resign, GetString(Resource.String.Menu_ResignGame_Text), isEnabled: enabled(Resource.Id.game_resign));
		sections.Add(game);

		// 8. アプリ設定（子項目1つ = グループクリックで直接遷移）
		var settings = new DrawerSectionModel("アプリ設定");
		settings.Add(Resource.Id.action_settings, GetString(Resource.String.action_settings), isEnabled: enabled(Resource.Id.action_settings));
		sections.Add(settings);

		// 9. 詳細操作
		var detail = new DrawerSectionModel("詳細操作");
		detail.Add(Resource.Id.file_load_last, GetString(Resource.String.Menu_FileLoadLast_Text), isEnabled: enabled(Resource.Id.file_load_last));
		detail.Add(Resource.Id.file_send, GetString(Resource.String.Menu_FileSend_Text), isEnabled: enabled(Resource.Id.file_send));
		detail.Add(Resource.Id.file_import, GetString(Resource.String.Menu_FileImport_Text), isEnabled: enabled(Resource.Id.file_import));
		detail.Add(Resource.Id.comment_edit, GetString(Resource.String.CommentMenuEdit_Text), isEnabled: enabled(Resource.Id.comment_edit));
		detail.Add(Resource.Id.comment_info_select, GetString(Resource.String.CommentInfoSelect_Text), isEnabled: enabled(Resource.Id.comment_info_select));
		detail.Add(Resource.Id.clear_all_comments, GetString(Resource.String.ClearAllComments_Text), isEnabled: () => true);
		detail.Add(Resource.Id.cmd_kyokumen, GetString(Resource.String.MenuKyokumen_Text), isEnabled: enabled(Resource.Id.cmd_kyokumen));
		detail.Add(Resource.Id.cmd_auto_play, GetString(Resource.String.MenuAutoPlay_Text), isEnabled: enabled(Resource.Id.cmd_auto_play));
		detail.Add(Resource.Id.engine_connection_settings, GetString(Resource.String.Menu_RemoteConnectionSettings_Text), isEnabled: enabled(Resource.Id.engine_connection_settings));
		detail.Add(Resource.Id.engine_install, GetString(Resource.String.Menu_EngineInstall_Text), isEnabled: enabled(Resource.Id.engine_install));
		detail.Add(Resource.Id.engine_folder, GetString(Resource.String.Menu_EngineFolder_Text), isEnabled: enabled(Resource.Id.engine_folder));
		detail.Add(Resource.Id.menu_about, GetString(Resource.String.Menu_About_Text), isEnabled: enabled(Resource.Id.menu_about));
		sections.Add(detail);

		return sections;
	}

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

	private void InitDrawer()
	{
		var sections = BuildDrawerSections();
		drawerAdapter_ = new DrawerSectionAdapter(this, sections);
		drawerListView_.SetAdapter(drawerAdapter_);
		drawerListView_.ExpandGroup(0);
		drawerListView_.SetOnGroupExpandListener(new SingleExpandListener(drawerListView_));
		drawerListView_.ChildClick += DrawerChildClick;
		drawerListView_.GroupClick += DrawerGroupClick;
	}

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
		commands.Add(CmdNo.FileSaveOverwrite, Resource.Id.file_save_overwrite, FileSaveOverwrite, CanSaveNotation);
		commands.Add(CmdNo.FileImport, Resource.Id.file_import, NotationSelectRequest, presenter.CanLoadNotaton);
		commands.Add(CmdNo.PositionLoad, Resource.Id.position_load, NotationSelectRequest, presenter.CanLoadNotaton);
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
		commands.Add(CmdNo.BookLoad, Resource.Id.book_load, BookLoadTree, presenter.CanLoadNotaton);
		commands.Add(CmdNo.BookBrowse, Resource.Id.book_browse, BookBrowse, presenter.CanLoadNotaton);
		commands.Add(CmdNo.MangeEgien, Resource.Id.menu_engine, () => { /* ドロワーから直接操作 */ }, presenter.CanManageEngine);
	}

	private void MenuGrayout(IMenu menu)
	{
		for (int i = 0; i < menu.Size(); i++)
		{
			IMenuItem item = menu.GetItem(i);
			item.SetEnabled(commands.IsEnable(item.ItemId));
		}
	}

	private void MainMenuGryout()
	{
		drawerAdapter_?.NotifyChanged();
	}

	private bool CanOpenDrawerItem(int itemId)
	{
		switch (itemId)
		{
		case Resource.Id.engine_select:
		case Resource.Id.engine_settings_wrapper:
		case Resource.Id.engine_options:
		case Resource.Id.engine_connection_settings:
		case Resource.Id.engine_install:
		case Resource.Id.engine_folder:
			return presenter.CanManageEngine();
		case Resource.Id.analysis_settings:
		case Resource.Id.display_settings:
		case Resource.Id.action_settings:
		case Resource.Id.menu_vastai:
		case Resource.Id.menu_about:
			return true;
		default:
			return commands.IsEnable(itemId);
		}
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
		ShowJointBoardDialog(new SNotation(notation), allowAddToNotation: false);
	}

	private void ShowJointBoardDialog(SNotation sNotation, bool allowAddToNotation)
	{
		if (sNotation == null)
		{
			return;
		}
		JointBoardDialog.NewInstance(presenter.Reverse, sNotation, allowAddToNotation).Show(FragmentManager, "JointBoardDialog");
	}

	public void AddJointBoardBranch(SNotation branchNotation)
	{
		presenter.AddBranch(branchNotation);
		MessagePopup(GetString(Resource.String.JointBoardAdded_Text), lengthShort: true);
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

	/// <summary>
	/// ファイル → 定跡ファイルを開く（ツリー展開モード、大きさ制限あり）
	/// </summary>
	private void BookLoadTree()
	{
		ShowOpenBookDialog(LocalFile.BookPath, browseMode: false);
	}

	/// <summary>
	/// メインメニュー → 定跡閲覧モード（サイズ制限なし）
	/// </summary>
	private void BookBrowse()
	{
		ShowOpenBookDialog(LocalFile.BookPath, browseMode: true);
	}

	private void StopBookBrowse()
	{
		Domain.Game.NotationModel.StopBookBrowse();
		bookBrowseCloseButton.Visibility = Android.Views.ViewStates.Gone;
		UpdateTopShortcutVisibility();
		presenter.InitNotation();
	}

	private void ShowOpenBookDialog(string path, bool browseMode)
	{
		OpenNotationDialog openDialog = OpenNotationDialog.NewInstance(path);
		OpenNotationDialog openNotationDialog = openDialog;
		openNotationDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(openNotationDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			LoadBookAsync(openDialog.FileName, browseMode);
		});
		openDialog.Show(FragmentManager, "OpenNotationDialog");
	}

	private CancellationTokenSource bookLoadCts;

	private const int TreeExpandMaxPositions = 100000;

	private void LoadBookAsync(string filename, bool browseMode)
	{
		var book = presenter.ParseBookFile(filename);
		if (book == null)
		{
			return;
		}

		var progressDialog = MyProgressDialog.NewInstance(Resource.String.BookLoading_Text);
		progressDialog.Show(FragmentManager, "BookLoadProgress");

		bookLoadCts?.Cancel();
		bookLoadCts = new CancellationTokenSource();
		var ct = bookLoadCts.Token;

		Task.Run(() =>
		{
			try
			{
				var hashBook = ShogiLib.BookExpander.BuildHashBook(book);
				AppDebug.Log.Info($"BookLoad: {hashBook.Count} positions hashed, browseMode={browseMode}");

				if (ct.IsCancellationRequested) return;

				if (!browseMode)
				{
					// ★ ツリー展開モード
					RunOnUiThread(() =>
					{
						try { progressDialog.SetMessage("ツリー展開中..."); } catch { }
					});

					ShogiLib.BookExpander.OnProgress = (nodes) =>
					{
						RunOnUiThread(() =>
						{
							try { progressDialog.SetMessage($"展開中: {nodes} ノード"); } catch { }
						});
					};

					ExpandTreeDirect(Domain.Game.NotationModel.Notation, hashBook, ct);
					ShogiLib.BookExpander.OnProgress = null;

					RunOnUiThread(() =>
					{
						Domain.Game.NotationModel.OnBookLoaded();
						try { progressDialog.Progress = 100; progressDialog.Dismiss(); } catch { }
						PopupNotationInfo();
					});
				}
				else
				{
					// ★ 定跡閲覧モード
					if (!browseMode)
					{
						AppDebug.Log.Info($"BookLoad: {hashBook.Count} positions exceeds limit -> browse mode");
					}

					RunOnUiThread(() =>
					{
						presenter.StartBookBrowse(hashBook);
						bookBrowseCloseButton.Visibility = Android.Views.ViewStates.Visible;
						UpdateTopShortcutVisibility();
						try { progressDialog.Progress = 100; progressDialog.Dismiss(); } catch { }
						if (!browseMode)
						{
							MessagePopup("定跡が大きいため、閲覧モードで開きました");
						}
					});
				}
			}
			catch (Exception ex)
			{
				AppDebug.Log.Error($"BookLoad: {ex.Message}");
				RunOnUiThread(() =>
				{
					try { progressDialog.Dismiss(); } catch { }
				});
			}
		}, ct);
	}

	/// <summary>
	/// 小規模定跡: DFSで直接ツリー構築（再帰版、10万局面以下用）
	/// </summary>
	private static void ExpandTreeDirect(
		ShogiLib.SNotation notation,
		Dictionary<ShogiLib.HashKey, List<ShogiLib.BookMove>> hashBook,
		CancellationToken ct)
	{
		notation.Init();
		notation.InitHashKey();

		int nodeCount = 0;
		var visited = new HashSet<ShogiLib.HashKey>();
		visited.Add(notation.Position.HashKey);

		ExpandDFS(notation, hashBook, visited, ref nodeCount, 0, ct);
		notation.First();

		AppDebug.Log.Info($"ExpandTreeDirect: {nodeCount} nodes");
	}

	private static void ExpandDFS(
		ShogiLib.SNotation notation,
		Dictionary<ShogiLib.HashKey, List<ShogiLib.BookMove>> book,
		HashSet<ShogiLib.HashKey> visited,
		ref int nodeCount,
		int depth,
		CancellationToken ct)
	{
		if (ct.IsCancellationRequested) return;

		ShogiLib.HashKey posKey = notation.Position.HashKey;

		if (book.TryGetValue(posKey, out var bookMoves))
		{
			var sorted = bookMoves.OrderBy(m => m.Count).ToList();
			foreach (var bm in sorted)
			{
				if (ct.IsCancellationRequested) return;

				var moveData = ShogiLib.Sfen.ParseMove(notation.Position, bm.UsiMove);
				if (moveData == null || moveData.MoveType == ShogiLib.MoveType.NoMove) continue;

				moveData.Score = bm.Eval;
				if (bm.Count > 0 || bm.Depth > 0)
				{
					moveData.CommentList = new System.Collections.Generic.List<string>();
					moveData.CommentList.Add($"出現回数: {bm.Count}  評価値: {bm.Eval}  深さ: {bm.Depth}");
				}

				bool added = notation.AddMove(moveData, ShogiLib.MoveAddMode.MERGE, changeChildCurrent: true);
				if (!added) continue;

				nodeCount++;
				if (nodeCount % 1000 == 0)
					ShogiLib.BookExpander.OnProgress?.Invoke(nodeCount);

				var nextKey = notation.Position.HashKey;
				if (!visited.Contains(nextKey))
				{
					visited.Add(nextKey);
					ExpandDFS(notation, book, visited, ref nodeCount, depth + 1, ct);
					visited.Remove(nextKey);
				}

				notation.MoveParent();
			}
		}
		else
		{
			// 橋渡し
			BridgeDFS(notation, book, visited, ref nodeCount, depth, ct);
		}
	}

	private static void BridgeDFS(
		ShogiLib.SNotation notation,
		Dictionary<ShogiLib.HashKey, List<ShogiLib.BookMove>> book,
		HashSet<ShogiLib.HashKey> visited,
		ref int nodeCount,
		int depth,
		CancellationToken ct)
	{
		var pos = notation.Position;
		var turn = pos.Turn;
		var turnFlag = ShogiLib.PieceExtensions.PieceFlagFromColor(turn);
		var bridgeMoves = new List<ShogiLib.MoveDataEx>();

		for (int from = 0; from < 81; from++)
		{
			var piece = pos.GetPiece(from);
			if (piece == ShogiLib.Piece.NoPiece || piece.ColorOf() != turn) continue;

			for (int to = 0; to < 81; to++)
			{
				if (from == to) continue;
				var target = pos.GetPiece(to);
				if (target != ShogiLib.Piece.NoPiece && target.ColorOf() == turn) continue;

				var baseMove = new ShogiLib.MoveData(ShogiLib.MoveType.MoveFlag, from, to, piece, target);
				bool canProm = ShogiLib.MoveCheck.CanPromota(baseMove);
				bool forceProm = canProm && ShogiLib.MoveCheck.ForcePromotion(piece, to);

				if (canProm)
				{
					var pm = new ShogiLib.MoveData(ShogiLib.MoveType.MoveMask, from, to, piece, target);
					if (ShogiLib.MoveCheck.IsValidLight(pos, pm))
						TryBridgeCollect(pos, book, bridgeMoves, pm);
				}
				if (!forceProm)
				{
					if (ShogiLib.MoveCheck.IsValidLight(pos, baseMove))
						TryBridgeCollect(pos, book, bridgeMoves, baseMove);
				}
			}
		}

		for (int pt = (int)ShogiLib.PieceType.HI; pt >= (int)ShogiLib.PieceType.FU; pt--)
		{
			var pieceType = (ShogiLib.PieceType)pt;
			if (!pos.IsHand(turn, pieceType)) continue;
			var dropPiece = (ShogiLib.Piece)((uint)pieceType | (uint)turnFlag);
			for (int to = 0; to < 81; to++)
			{
				if (pos.GetPiece(to) != ShogiLib.Piece.NoPiece) continue;
				var move = new ShogiLib.MoveData(ShogiLib.MoveType.DropFlag, 0, to, dropPiece, ShogiLib.Piece.NoPiece);
				if (ShogiLib.MoveCheck.IsValidLight(pos, move))
					TryBridgeCollect(pos, book, bridgeMoves, move);
			}
		}

		foreach (var moveData in bridgeMoves)
		{
			if (ct.IsCancellationRequested) return;
			bool added = notation.AddMove(moveData, ShogiLib.MoveAddMode.MERGE, changeChildCurrent: true);
			if (!added) continue;
			nodeCount++;
			if (nodeCount % 1000 == 0)
				ShogiLib.BookExpander.OnProgress?.Invoke(nodeCount);

			var nextKey = notation.Position.HashKey;
			if (!visited.Contains(nextKey))
			{
				visited.Add(nextKey);
				ExpandDFS(notation, book, visited, ref nodeCount, depth + 1, ct);
				visited.Remove(nextKey);
			}
			notation.MoveParent();
		}
	}

	private static void TryBridgeCollect(
		ShogiLib.SPosition pos, Dictionary<ShogiLib.HashKey, List<ShogiLib.BookMove>> book,
		List<ShogiLib.MoveDataEx> result, ShogiLib.MoveData move)
	{
		if (move.MoveType.HasFlag(ShogiLib.MoveType.MoveFlag) && !move.MoveType.HasFlag(ShogiLib.MoveType.DropFlag))
			move.CapturePiece = pos.GetPiece(move.ToSquare);

		pos.Move(move);
		bool hit = book.ContainsKey(pos.HashKey);
		pos.UnMove(move, null);

		if (hit)
			result.Add(new ShogiLib.MoveDataEx(move));
	}

	private void FileSave()
	{
		ShowSaveNotationDialog(LocalFile.KifPath, (presenter.NotationFileName != string.Empty) ? presenter.NotationFileName : presenter.NotationNewFileName, notation.BlackName, notation.WhiteName);
	}

	private void FileSaveOverwrite()
	{
		if (string.IsNullOrEmpty(presenter.NotationFileName))
		{
			FileSave();
			return;
		}

		presenter.SaveNotation(LocalFile.KifPath, presenter.NotationFileName);
		MessagePopup(Resource.String.Saved_Text);
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
		InitDrawer();

		// vast.ai watchdog: 解析終了後のアイドルで全インスタンスを自動終了
		VastAiWatchdog.Instance.InstanceAutoStopped -= OnVastAiAutoStopped;
		VastAiWatchdog.Instance.InstanceAutoStopped += OnVastAiAutoStopped;
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
		Intent = intent;
		if (intent != null && presenter.LoadNotationFromWeb(intent.DataString))
		{
			PopupNotationInfo();
		}
	}

	private void InitUI()
	{
		UpdateWindowSettings();
		SetContentView(Resource.Layout.maina);
		FontUtil.ApplyFont(FindViewById(Android.Resource.Id.Content));
		shogiBoard = FindViewById<ShogiBoard>(Resource.Id.shogiboard);
		shogiBoard.MakeMoveEvent += ShogiBoard_MakeMoveEvent;
		shogiBoard.AnimationEnd += ShogiBoard_AnimationEnd;
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
		// ボタンサイズとテキスト幅を初期レイアウト後に固定
		analyzButton.Post(() =>
		{
			var lp = analyzButton.LayoutParameters;
			lp.Width = analyzButton.Width;
			lp.Height = analyzButton.Height;
			analyzButton.LayoutParameters = lp;
			// テキスト幅を固定してアイコン位置がずれないようにする
			if (analyzeText != null)
			{
				var tlp = analyzeText.LayoutParameters;
				tlp.Width = analyzeText.Width;
				analyzeText.LayoutParameters = tlp;
				analyzeText.Gravity = Android.Views.GravityFlags.Center;
			}
		});
		menuButton = FindViewById<ImageButton>(Resource.Id.menu_button);
		menuButton.Click += MenuButton_Click;
		FindViewById<ImageButton>(Resource.Id.camera_read).Click += (s, e) => CameraRead();
		bookBrowseCloseButton = FindViewById<Button>(Resource.Id.book_browse_close);
		bookBrowseCloseButton.Click += (s, e) => StopBookBrowse();
		topShortcutBar = FindViewById(Resource.Id.top_shortcut_bar);
		topShortcutButtons = FindViewById(Resource.Id.top_shortcut_buttons);
		reverseButton = FindViewById<ImageButton>(Resource.Id.reverse_button);
		reverseButton.Click += ReverseButton_Click;
		passButton = FindViewById<ImageButton>(Resource.Id.pass_button);
		passButton.Click += (s, e) => presenter.Pass();
		FindViewById<ImageButton>(Resource.Id.board_edit_button).Click += (s, e) => BoardEdit();
		inputCancelButton = FindViewById<ImageButton>(Resource.Id.input_cancel_button);
		inputCancelButton.Click += InputCancelButton_Click;
		if (Settings.AppSettings.ReverseButotn != 50)
		{
			reverseButton.SetImageResource(Resource.Drawable.ic_fn);
		}
			stateText = FindViewById<TextView>(Resource.Id.state_text);
			stateText.Click += NotationText_Click;
			threatmateBadge = FindViewById<TextView>(Resource.Id.threatmate_badge);
			if (threatmateBadge != null)
			{
				threatmateBadge.Click += ThreatmateBadge_Click;
			}
			drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
		drawerLayout.DrawerOpened += DrawerLayout_DrawerOpened;
		drawerLayout.DrawerSlide += DrawerLayout_DrawerSlide;
		drawerLayout.LayoutChange += DrawerLayout_LayoutChange;
		leftDrawer = FindViewById<LinearLayout>(Resource.Id.left_drawer);
		drawerListView_ = FindViewById<ExpandableListView>(Resource.Id.main_manu_lsist_view);
		// ドロワーのアダプター設定はpresenter初期化後にInitDrawer()で行う
		TextView textView = FindViewById<TextView>(Resource.Id.app_name);
		AssemblyName name = Assembly.GetExecutingAssembly().GetName();
		textView.Text = "ShogiDroid ver " + name.Version;
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
		notationAdapter = new NotationAdapter(this);
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
		UpdateTopShortcutVisibility();
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
		infoPageAdepter.DispPolicyPage = Settings.AppSettings.AutoPolicyAnalysis;
		infoPager.OffscreenPageLimit = 4;
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

	public override bool DispatchTouchEvent(Android.Views.MotionEvent e)
	{
#if DEBUG
		if (e.GetToolType(0) == Android.Views.MotionEventToolType.Stylus)
		{
			AppDebug.Log.Info($"SPen: action={e.Action} x={e.GetX():F0} y={e.GetY():F0}");
		}
#endif
		return base.DispatchTouchEvent(e);
	}

	protected override void OnResume()
	{
		base.OnResume();
		isActivityVisible_ = true;
		PlaySE.Initialize(this);
		presenter.Resume();
		CancelBackgroundAnalysisNotification();
		if (notation != null)
		{
			UpdateNotation(NotationEventId.OBJECT_CHANGED);
			UpdateInfo(presenter.PvInfos);
			UpdateHitInfo();
			UpdateState();
		}
#if DEBUG
		RegisterDebugReceiver();
#endif
	}

	protected override void OnPause()
	{
#if DEBUG
		UnregisterDebugReceiver();
#endif
		isActivityVisible_ = false;
		DismissVastAiBootDialog();
		base.OnPause();
		shogiBoard.AnimationStop();
		StoreSettings();
		presenter.Pause();
		UpdateBackgroundAnalysisNotification();
		PlaySE.Destory();
	}

	protected override void OnDestroy()
	{
		isDestroyed_ = true;
		CancelAutoBootVastAi();
		VastAiWatchdog.Instance.InstanceAutoStopped -= OnVastAiAutoStopped;
		base.OnDestroy();
		CancelBackgroundAnalysisNotification();
		StopRemoteMonitor();
		presenter.Destory();
		PlaySE.Destory();
	}

	public override void OnConfigurationChanged(Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		StoreSettings();
		InitUI();
		InitDrawer();
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
			MainMenuGryout();
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
			SetAnalyzeTimeText("00:00");
			analyzeStartTime = DateTime.Now;
			StartAnalyzeTimer();
		}
		else
		{
			analyzButton.SetBackgroundResource(Resource.Drawable.analyze_btn_bg_idle);
			if (analyzeText != null)
			{
				if (analyzeTextDefaultPaddingTop >= 0)
					analyzeText.SetPadding(analyzeText.PaddingLeft,
						analyzeTextDefaultPaddingTop,
						analyzeText.PaddingRight, analyzeText.PaddingBottom);
				analyzeText.Text = "解析開始";
			}
			StopAnalyzeTimer();
		}
	}

	private int analyzeTextDefaultPaddingTop = -1;

	private void SetAnalyzeTimeText(string time)
	{
		if (analyzeText == null) return;
		// 初回に元のpaddingTopを記録
		if (analyzeTextDefaultPaddingTop < 0)
			analyzeTextDefaultPaddingTop = analyzeText.PaddingTop;
		// 2行テキストを少し上にずらして視覚的に中央に見せる
		int offset = (int)(2 * Resources.DisplayMetrics.Density);
		analyzeText.SetPadding(analyzeText.PaddingLeft,
			analyzeTextDefaultPaddingTop - offset,
			analyzeText.PaddingRight, analyzeText.PaddingBottom);

		string full = $"解析中\n{time}";
		var spannable = new Android.Text.SpannableString(full);
		int timeStart = full.IndexOf('\n') + 1;
		spannable.SetSpan(
			new Android.Text.Style.AbsoluteSizeSpan(10, true),
			timeStart, full.Length, Android.Text.SpanTypes.ExclusiveExclusive);
		spannable.SetSpan(
			new Android.Text.Style.ForegroundColorSpan(Android.Graphics.Color.Argb(204, 255, 255, 255)),
			timeStart, full.Length, Android.Text.SpanTypes.ExclusiveExclusive);
		analyzeText.SetText(spannable, Android.Widget.TextView.BufferType.Spannable);
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
					SetAnalyzeTimeText($"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}");
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
			OpenSettingsHome();
			break;
		case Resource.Id.menu_vastai:
			StartActivityForResult(new Intent(this, typeof(CloudActivity)), VASTAI_ACTIVITY_CODE);
			drawerLayout.CloseDrawers();
			break;
		case Resource.Id.analysis_settings:
			OpenSettingsSection(SettingActivity.SectionAnalyze);
			break;
		case Resource.Id.display_settings:
			OpenSettingsSection(SettingActivity.SectionDisplay);
			break;
		case Resource.Id.engine_select:
			if (!CanOpenDrawerItem(itemId))
			{
				return false;
			}
			ShowEngineSelectDialog();
			break;
		case Resource.Id.engine_settings_wrapper:
			if (!CanOpenDrawerItem(itemId))
			{
				return false;
			}
			StartActivityForResult(new Intent(this, typeof(EngineSettingsWrapperActivity)), 110);
			break;
		case Resource.Id.engine_options:
			if (!CanOpenDrawerItem(itemId))
			{
				return false;
			}
			StartActivityForResult(new Intent(this, typeof(EngineOptionsActivity)), 104);
			break;
		case Resource.Id.engine_connection_settings:
			if (!CanOpenDrawerItem(itemId))
			{
				return false;
			}
			OpenSettingsSection(SettingActivity.SectionEngineConnection);
			break;
		case Resource.Id.engine_install:
			if (!CanOpenDrawerItem(itemId))
			{
				return false;
			}
			StartActivityForResult(new Intent(this, typeof(EngineInstallActivity)), 107);
			break;
		case Resource.Id.engine_folder:
			if (!CanOpenDrawerItem(itemId))
			{
				return false;
			}
			ShowSelectEngineFolderDialog();
			break;
		case Resource.Id.cmd_export_board_image:
			ExportBoardImage();
			drawerLayout.CloseDrawers();
			break;
		case Resource.Id.menu_about:
			MessagePopup(GetString(Resource.String.app_name) + " ver " + Assembly.GetExecutingAssembly().GetName().Version, lengthShort: true);
			break;
		case Resource.Id.clear_all_comments:
			ConfirmClearAllComments();
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

	private void ConfirmClearAllComments()
	{
		drawerLayout.CloseDrawers();
		new Android.App.AlertDialog.Builder(this)
			.SetMessage(GetString(Resource.String.ClearAllCommentsConfirm_Text))
			.SetPositiveButton(Android.Resource.String.Ok, (s, a) =>
			{
				presenter.ClearAllComments();
				UpdateNotation(NotationEventId.COMMENT);
			})
			.SetNegativeButton(Android.Resource.String.Cancel, (s, a) => { })
			.Show();
	}

	private void OpenSettingsHome()
	{
		Settings.Save();
		StartActivityForResult(new Intent(this, typeof(SettingsHomeActivity)), 102);
		drawerLayout.CloseDrawers();
	}

	private void OpenSettingsSection(string section)
	{
		Settings.Save();
		var intent = new Intent(this, typeof(SettingActivity));
		intent.PutExtra(SettingActivity.ExtraSection, section);
		StartActivityForResult(intent, 102);
		drawerLayout.CloseDrawers();
	}

	private void DrawerChildClick(object sender, ExpandableListView.ChildClickEventArgs e)
	{
		var item = drawerAdapter_.GetItemModel(e.GroupPosition, e.ChildPosition);
		if (item != null && CanOpenDrawerItem(item.Id))
		{
			MenuItemSelected(item.Id);
		}
	}

	private void DrawerGroupClick(object sender, ExpandableListView.GroupClickEventArgs e)
	{
		var section = drawerAdapter_.GetSectionModel(e.GroupPosition);
		if (e.GroupPosition == 0)
		{
			drawerListView_.ExpandGroup(0);
			e.Handled = true;
			return;
		}
		if (section.Items.Count == 1)
		{
			// 子項目1つだけのセクションは直接実行
			if (CanOpenDrawerItem(section.Items[0].Id))
			{
				MenuItemSelected(section.Items[0].Id);
			}
			e.Handled = true;
		}
		else if (section.Items.Count == 0)
		{
			e.Handled = true;
		}
		else
		{
			e.Handled = false; // 通常の展開/折りたたみ
		}
	}

	private class SingleExpandListener : Java.Lang.Object, ExpandableListView.IOnGroupExpandListener
	{
		private readonly ExpandableListView list_;
		private int lastExpanded_ = 0;
		public SingleExpandListener(ExpandableListView list) { list_ = list; }
		public void OnGroupExpand(int groupPosition)
		{
			if (groupPosition == 0) return; // クイック操作は常時展開
			if (lastExpanded_ != groupPosition && lastExpanded_ != 0)
				list_.CollapseGroup(lastExpanded_);
			lastExpanded_ = groupPosition;
		}
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
			ShowJointBoardDialog(sNotation, allowAddToNotation: true);
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
		// 解析中でない時はコメントに残っている過去の解析結果を表示
		if (!presenter.ComState.IsThinking())
		{
			infoPageAdepter.SetCommentAnalysis(notation.MoveCurrent.CommentList);
		}
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
			stateText.Text = BuildStateText();
		}
		UpdateThreatmateBadge();
		UpdateAnalyzeButton(presenter.ComState == ComputerState.Analyzing || presenter.ComState == ComputerState.Mating);
		if (evalGraphView != null)
		{
			evalGraphView.DispComGraph = presenter.GameMode != GameMode.Play || Settings.AppSettings.ShowComputerThinking || presenter.BothComputer;
		}
		UpdateScreenOn();
		UpdateBackgroundAnalysisNotification();
	}

	private void UpdateBackgroundAnalysisNotification()
	{
		if (isActivityVisible_ || !ShouldShowBackgroundAnalysisNotification())
		{
			CancelBackgroundAnalysisNotification();
			return;
		}

		StartBackgroundAnalysisService(presenter.GameMode == GameMode.Consider);
	}

	private bool ShouldShowBackgroundAnalysisNotification()
	{
		if (Settings.EngineSettings.EngineNo != RemoteEnginePlayer.RemoteEngineNo)
		{
			return false;
		}

		if (presenter.GameMode != GameMode.Analyzer && presenter.GameMode != GameMode.Consider)
		{
			return false;
		}

		return presenter.Busy || presenter.ComState.IsThinking();
	}

	private PendingIntent BuildOpenMainActivityPendingIntent()
	{
		var openIntent = new Intent(this, typeof(MainActivity));
		openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
		return PendingIntent.GetActivity(
			this,
			2,
			openIntent,
			PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
	}

	private void CreateBackgroundAnalysisNotificationChannel()
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.O)
		{
			return;
		}

		var manager = (NotificationManager)GetSystemService(NotificationService);
		if (manager.GetNotificationChannel(BACKGROUND_ANALYSIS_NOTIFICATION_CHANNEL_ID) != null)
		{
			return;
		}

		var channel = new NotificationChannel(
			BACKGROUND_ANALYSIS_NOTIFICATION_CHANNEL_ID,
			GetString(Resource.String.BackgroundAnalyzeNotificationChannel_Text),
			NotificationImportance.Low)
		{
			Description = GetString(Resource.String.BackgroundAnalyzeNotificationChannelDescription_Text)
		};
		manager.CreateNotificationChannel(channel);
	}

	private void CancelBackgroundAnalysisNotification()
	{
		StopBackgroundAnalysisService();
		NotificationManagerCompat.From(this).Cancel(BACKGROUND_ANALYSIS_NOTIFICATION_ID);
	}

	private void StartBackgroundAnalysisService(bool isConsider)
	{
		var serviceIntent = new Intent(this, typeof(BackgroundAnalysisService));
		serviceIntent.SetAction(BackgroundAnalysisService.ActionStart);
		serviceIntent.PutExtra(BackgroundAnalysisService.ExtraIsConsider, isConsider);
		ContextCompat.StartForegroundService(this, serviceIntent);
	}

	private void StopBackgroundAnalysisService()
	{
		StopService(new Intent(this, typeof(BackgroundAnalysisService)));
	}

	public void UpdatePolicyInfo(PolicyInfo policyInfo)
	{
		RunOnUiThread(() =>
		{
			infoPageAdepter?.SetPolicyInfo(policyInfo);
		});
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
			menuButton.SetImageResource(Resource.Drawable.ic_menu_hamburger);
		}
	}

	public void ShowInterstitial()
	{
		// Removed: AdMob dependency not available
	}

	private void ExportBoardImage()
	{
		try
		{
			var notation = presenter.Notation;
			if (notation == null)
			{
				Toast.MakeText(this, "局面がありません", ToastLength.Short).Show();
				return;
			}

			string blackName = notation.BlackName ?? "先手";
			string whiteName = notation.WhiteName ?? "後手";
			string header = notation.MoveCurrent.Number > 0
				? $"第{notation.MoveCurrent.Number}手"
				: null;

			var bmp = BoardImageExporter.Generate(
				notation.Position, blackName, whiteName, header, 1200, FontUtil.Normal);

			// Pictures/ShogiDroid に保存
			string dir = System.IO.Path.Combine(
				Android.OS.Environment.GetExternalStoragePublicDirectory(
					Android.OS.Environment.DirectoryPictures).AbsolutePath,
				"ShogiDroid");
			string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			string path = BoardImageExporter.SaveToFile(bmp, dir, $"board_{timestamp}.png");
			bmp.Recycle();

			// メディアスキャンで通知
			var mediaScanIntent = new Intent(Intent.ActionMediaScannerScanFile);
			mediaScanIntent.SetData(Android.Net.Uri.FromFile(new Java.IO.File(path)));
			SendBroadcast(mediaScanIntent);

			Toast.MakeText(this, $"保存: {path}", ToastLength.Long).Show();
		}
		catch (Exception ex)
		{
			Toast.MakeText(this, $"エラー: {ex.Message}", ToastLength.Long).Show();
		}
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

	private string BuildStateText()
	{
		return ComStateToString(presenter.ComState);
	}

	private void ThreatmateBadge_Click(object sender, EventArgs e)
	{
		presenter.AutoPlayStop();
		if (presenter.ThreatmateInfo?.State == ThreatmateState.Threatmate)
		{
			SNotation sNotation = presenter.LoadThreatmate();
			if (sNotation != null)
			{
				ShowJointBoardDialog(sNotation, allowAddToNotation: false);
				return;
			}
			MessagePopup(GetString(Resource.String.ThreatmateLineUnavailable_Text), lengthShort: true);
			return;
		}
		string description = GetThreatmateDescription(presenter.ThreatmateInfo);
		if (!string.IsNullOrEmpty(description))
		{
			MessagePopup(description, lengthShort: true);
		}
	}

	private void UpdateThreatmateBadge()
	{
		if (threatmateBadge == null)
		{
			return;
		}
		if (!Settings.AppSettings.AutoThreatmateAnalysis)
		{
			threatmateBadge.Visibility = ViewStates.Gone;
			threatmateBadge.ContentDescription = null;
			return;
		}

		ThreatmateInfo info = presenter.ThreatmateInfo;
		switch (info?.State ?? ThreatmateState.None)
		{
		case ThreatmateState.Analyzing:
			SetThreatmateBadge("…", Resource.Drawable.threatmate_badge_neutral_bg, Resource.Color.threatmate_badge_neutral_text, GetString(Resource.String.ThreatmateAnalyzing_Text));
			return;
		case ThreatmateState.Threatmate:
			SetThreatmateBadge("!", Resource.Drawable.threatmate_badge_bg, Resource.Color.threatmate_badge_text, GetThreatmateDescription(info));
			return;
		case ThreatmateState.Unknown:
			SetThreatmateBadge("?", Resource.Drawable.threatmate_badge_neutral_bg, Resource.Color.threatmate_badge_neutral_text, GetString(Resource.String.ThreatmateUnknown_Text));
			return;
		default:
			threatmateBadge.Visibility = ViewStates.Gone;
			threatmateBadge.ContentDescription = null;
			return;
		}
	}

	private void SetThreatmateBadge(string text, int backgroundResId, int textColorResId, string description)
	{
		threatmateBadge.Text = text;
		threatmateBadge.SetBackgroundResource(backgroundResId);
		threatmateBadge.SetTextColor(new Color(ContextCompat.GetColor(this, textColorResId)));
		threatmateBadge.ContentDescription = description;
		threatmateBadge.Visibility = ViewStates.Visible;
	}

	private string GetThreatmateDescription(ThreatmateInfo info)
	{
		if (info == null)
		{
			return string.Empty;
		}
		switch (info.State)
		{
		case ThreatmateState.Analyzing:
			return GetString(Resource.String.ThreatmateAnalyzing_Text);
		case ThreatmateState.Threatmate:
			string threatmateText = GetString(Resource.String.Threatmate_Text);
			if (info.MatePly > 0)
			{
				return $"{threatmateText} ({info.MatePly}手詰)";
			}
			return threatmateText;
		case ThreatmateState.NoThreatmate:
			return GetString(Resource.String.ThreatmateNone_Text);
		case ThreatmateState.Unknown:
			return GetString(Resource.String.ThreatmateUnknown_Text);
		default:
			return string.Empty;
		}
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
			LoadNotationOrPositionCollection(openDialog.FileName);
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
			LoadNotationOrPositionCollection(path);
			return;
		}
		string text = LoadTextFile(uri);
		if (!string.IsNullOrEmpty(text))
		{
			string sourceName = Util.GetFileName(this, uri);
			if (!TryLoadPositionCollection(sourceName, text))
			{
				presenter.PasteNotation(text);
				PopupNotationInfo();
			}
		}
	}

	private void LoadNotationOrPositionCollection(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return;
		}

		string sourceName = System.IO.Path.GetFileName(path);
		try
		{
			string text = StringUtil.Load(path, StringUtil.GetEncording(path));
			if (TryLoadPositionCollection(sourceName, text))
			{
				return;
			}
		}
		catch
		{
		}

		if (presenter.LoadNotation(path))
		{
			PopupNotationInfo();
		}
	}

	private bool TryLoadPositionCollection(string sourceName, string text)
	{
		List<PositionCollectionEntry> entries = PositionCollectionParser.Parse(text);
		if (entries.Count == 0)
		{
			return false;
		}

		if (entries.Count == 1)
		{
			LoadPositionCollectionEntry(sourceName, entries, 0);
			return true;
		}

		PositionCollectionDialog dialog = PositionCollectionDialog.NewInstance(sourceName, entries.Count);
		PositionCollectionDialog positionCollectionDialog = dialog;
		positionCollectionDialog.OKClick = (EventHandler<EventArgs>)Delegate.Combine(positionCollectionDialog.OKClick, (EventHandler<EventArgs>)delegate
		{
			LoadPositionCollectionEntry(sourceName, entries, dialog.SelectedIndex);
		});
		dialog.Show(FragmentManager, "PositionCollectionDialog");
		return true;
	}

	private void LoadPositionCollectionEntry(string sourceName, List<PositionCollectionEntry> entries, int index)
	{
		if (index < 0 || index >= entries.Count)
		{
			return;
		}

		if (presenter.LoadNotationFromString(sourceName, entries[index].Sfen))
		{
			MessagePopup(string.Format(
				GetString(Resource.String.PositionCollectionLoaded_Text),
				string.IsNullOrEmpty(sourceName) ? GetString(Resource.String.OpenDialogTitle_Text) : sourceName,
				index + 1,
				entries.Count));
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
			Intent intent = new Intent(Intent.ActionView);
			Android.Net.Uri data;
			using (Java.IO.File file = new Java.IO.File(LocalFile.KifPath))
			{
				data = Build.VERSION.SdkInt >= BuildVersionCodes.N
					? FileProvider.GetUriForFile(this, PackageName + ".provider", file)
					: Android.Net.Uri.FromFile(file);
			}
			intent.SetDataAndType(data, "vnd.android.document/directory");
			intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPrefixUriPermission);
			intent.ClipData = ClipData.NewRawUri(GetString(Resource.String.Menu_OpenKifuFolder_Text), data);
			intent.PutExtra("org.openintents.extra.ABSOLUTE_PATH", LocalFile.KifPath + Java.IO.File.Separator);
			try
			{
				StartActivity(Intent.CreateChooser(intent, GetString(Resource.String.Menu_OpenKifuFolder_Text)));
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


	private const int VASTAI_ACTIVITY_CODE = 120;

	public void OnEngineInitialized()
	{
		// リモートエンジンの場合、RemoteMonitorを起動/確認
		if (Settings.EngineSettings.EngineNo == ShogiGUI.Engine.RemoteEnginePlayer.RemoteEngineNo)
			StartRemoteMonitor();
		else
			StopRemoteMonitor();
	}

	private void OnVastAiAutoStopped()
	{
		RunOnUiThread(() =>
		{
			Toast.MakeText(this, "vast.ai インスタンスをアイドルのため一時停止しました", ToastLength.Long).Show();
		});
	}

	private bool HasActiveVastAiBoot()
	{
		return vastAiBootTask_ != null && !vastAiBootTask_.IsCompleted;
	}

	private void CancelAutoBootVastAi()
	{
		if (vastAiBootCts_ != null && !vastAiBootCts_.IsCancellationRequested)
		{
			vastAiBootCts_.Cancel();
		}

		DismissVastAiBootDialog();
	}

	private void CompleteAutoBootVastAi(CancellationTokenSource cts)
	{
		if (ReferenceEquals(vastAiBootCts_, cts))
		{
			vastAiBootCts_ = null;
			vastAiBootTask_ = null;
		}

		cts.Dispose();
	}

	private void RunOnUiThreadIfAlive(CancellationToken ct, Action action)
	{
		if (ct.IsCancellationRequested || isDestroyed_ || IsFinishing)
		{
			return;
		}

		RunOnUiThread(() =>
		{
			if (ct.IsCancellationRequested || isDestroyed_ || IsFinishing)
			{
				return;
			}

			action();
		});
	}

	private void RunOnUiThreadIfVisible(CancellationToken ct, Action action)
	{
		RunOnUiThreadIfAlive(ct, () =>
		{
			if (!isActivityVisible_)
			{
				return;
			}

			action();
		});
	}

	private IProgress<string> CreateVastAiBootProgress(CancellationToken ct)
	{
		return new Progress<string>(msg =>
		{
			RunOnUiThreadIfVisible(ct, () => SetStatusText(msg));
		});
	}

	public void OnVastAiBootRequired()
	{
		RunOnUiThread(() =>
		{
			if (HasActiveVastAiBoot())
			{
				AppDebug.Log.Info("AutoBootVastAi: boot already in progress");
				return;
			}

			var cts = new CancellationTokenSource();
			vastAiBootCts_ = cts;
			vastAiBootTask_ = AutoBootVastAiAsync(cts);
		});
	}

	private AlertDialog vastAiBootDialog_;
	private TextView vastAiBootStatusText_;

	private void ShowVastAiBootDialog(string message)
	{
		DismissVastAiBootDialog();
		var layout = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
		layout.SetPadding(48, 32, 48, 16);

		vastAiBootStatusText_ = new TextView(this) { Text = message };
		vastAiBootStatusText_.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		layout.AddView(vastAiBootStatusText_);

		var bar = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal);
		bar.Indeterminate = true;
		var barLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		barLp.TopMargin = 16;
		bar.LayoutParameters = barLp;
		layout.AddView(bar);

		vastAiBootDialog_ = new AlertDialog.Builder(this)
			.SetTitle("インスタンス起動中")
			.SetView(layout)
			.SetNegativeButton("キャンセル", (s, e) =>
			{
				CancelAutoBootVastAi();
				DismissVastAiBootDialog();
			})
			.SetCancelable(true)
			.Create();
		vastAiBootDialog_.CancelEvent += (s, e) =>
		{
			CancelAutoBootVastAi();
		};
		vastAiBootDialog_.Show();
	}

	private void DismissVastAiBootDialog()
	{
		try { vastAiBootDialog_?.Dismiss(); } catch { }
		vastAiBootDialog_ = null;
		vastAiBootStatusText_ = null;
	}

	private void SetStatusText(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			DismissVastAiBootDialog();
			return;
		}
		if (vastAiBootDialog_ == null)
		{
			ShowVastAiBootDialog(text);
		}
		else if (vastAiBootStatusText_ != null)
		{
			vastAiBootStatusText_.Text = text;
		}
	}

	private async System.Threading.Tasks.Task AutoBootVastAiAsync(CancellationTokenSource cts)
	{
		CancellationToken ct = cts.Token;
		string apiKey = Settings.EngineSettings.VastAiApiKey;
		int lastInstanceId = Settings.EngineSettings.VastAiInstanceId;

		if (string.IsNullOrEmpty(apiKey) || lastInstanceId <= 0)
		{
			Toast.MakeText(this, GetString(Resource.String.VastAiBootFailed_Text), ToastLength.Long).Show();
			return;
		}

		// プログレスダイアログを表示
		ShowVastAiBootDialog(GetString(Resource.String.VastAiBooting_Text));

		try
		{
			using var manager = new VastAiManager(apiKey);

			// 前回のインスタンスを探す
			var instances = await manager.ListInstancesAsync(ct);
			VastAiInstance target = null;
			foreach (var inst in instances)
			{
				if (inst.Id == lastInstanceId)
				{
					target = inst;
					break;
				}
			}

			var bootProgress = CreateVastAiBootProgress(ct);

			if (target != null && target.IsRunning && !string.IsNullOrEmpty(target.PublicIpAddr))
			{
				// 既に稼働中ならSSH待ちだけ行う
				AppDebug.Log.Info($"AutoBootVastAi: instance {lastInstanceId} は稼働中");
			}
			else if (target != null && (target.IsRunning || target.IsLoading))
			{
				RunOnUiThreadIfVisible(ct, () => SetStatusText("既存インスタンスの起動完了を待機中..."));
				target = await manager.WaitForReadyAsync(lastInstanceId, bootProgress, ct);
			}
			else if (target != null && target.IsStopped)
			{
				// 休止中なら再開
				RunOnUiThreadIfVisible(ct, () => SetStatusText("インスタンス再開中..."));
				await manager.StartInstanceAsync(lastInstanceId, ct);
				target = await manager.WaitForReadyAsync(lastInstanceId, bootProgress, ct);
			}
			else
			{
				// インスタンスが存在しない場合は新規検索＆作成
				RunOnUiThreadIfVisible(ct, () => SetStatusText("新規インスタンスを検索中..."));
				target = await SearchAndCreateInstance(manager, ct);
			}

			ct.ThrowIfCancellationRequested();

			if (target == null)
			{
				throw new VastAiException("インスタンスが見つかりません");
			}

			// まだ起動途中（Loading）の場合はリトライのために再取得
			if (!target.IsRunning || string.IsNullOrEmpty(target.PublicIpAddr))
			{
				RunOnUiThreadIfVisible(ct, () => SetStatusText("起動完了を待機中..."));
				target = await manager.WaitForReadyAsync(lastInstanceId, CreateVastAiBootProgress(ct), ct);
			}

			if (target == null || !target.IsRunning || string.IsNullOrEmpty(target.PublicIpAddr))
			{
				throw new VastAiException("インスタンスの起動がタイムアウトしました。クラウド画面から状態を確認してください。");
			}

			// エンジン接続設定を更新
			var (sshHost, sshPort) = target.GetSshEndpoint();
			Settings.EngineSettings.RemoteHost = sshHost;
			Settings.EngineSettings.VastAiSshPort = sshPort;
			Settings.EngineSettings.VastAiInstanceId = target.Id;
			Settings.EngineSettings.VastAiCpuCores = (int)target.CpuCoresEffective;
			Settings.EngineSettings.VastAiRamMb = (int)(target.CpuRamGb * 1024);
			Settings.EngineSettings.VastAiGpuRamMb = (int)(target.GpuRamGb * 1024);
			Settings.Save();

			// Watchdog を再開
			VastAiWatchdog.Instance.StartMonitoring(target.Id, apiKey);
			VastAiWatchdog.Instance.SaveLastConnectionInfo(
				target.Id, sshHost, sshPort,
				Settings.EngineSettings.VastAiSshEngineCommand);

			RunOnUiThreadIfAlive(ct, () =>
			{
				SetStatusText(string.Empty);
				// 保留中の操作を再開
				ShogiGUI.Domain.Game.ResumeAfterVastAiBoot();
			});
		}
		catch (System.OperationCanceledException)
		{
			AppDebug.Log.Info("AutoBootVastAi: cancelled");
		}
		catch (Exception ex)
		{
			AppDebug.Log.Error($"AutoBootVastAi: {ex.Message}");
			RunOnUiThreadIfVisible(ct, () =>
			{
				SetStatusText(string.Empty);
				Toast.MakeText(this, $"{GetString(Resource.String.VastAiBootFailed_Text)}: {ex.Message}", ToastLength.Long).Show();
			});
		}
		finally
		{
			CompleteAutoBootVastAi(cts);
		}
	}

	private async System.Threading.Tasks.Task<VastAiInstance> SearchAndCreateInstance(
		VastAiManager manager,
		CancellationToken ct)
	{
		// 前回の検索条件で新規インスタンスを作成
		var criteria = new VastAiSearchCriteria
		{
			GpuNames = Settings.EngineSettings.VastAiGpuNames?.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
				?? new[] { "RTX 4090" },
			MinCpuCoresEffective = Settings.EngineSettings.VastAiMinCpuCores,
			MaxDphTotal = Settings.EngineSettings.VastAiMaxDph,
			MinCudaVersion = Settings.EngineSettings.VastAiMinCudaVersion,
			RentType = "bid"
		};

		var offers = await manager.SearchOffersAsync(criteria, ct);
		if (offers == null || offers.Count == 0)
			throw new VastAiException("条件に合うオファーが見つかりません");

		var offer = offers[0]; // 最安値
		RunOnUiThreadIfVisible(ct, () => SetStatusText($"インスタンス作成中... ({offer.GpuName})"));

		var config = new VastAiInstanceConfig
		{
			DockerImage = Settings.EngineSettings.VastAiDockerImage,
			Ports = Array.Empty<int>(),
			DiskGb = 8.0,
			OnStartCmd = Settings.EngineSettings.VastAiOnStartCmd,
			BidPrice = offer.DphTotal
		};

		int newId = await manager.CreateInstanceAsync(offer.Id, config, ct);
		Settings.EngineSettings.VastAiInstanceId = newId;
		Settings.Save();

		return await manager.WaitForReadyAsync(newId, CreateVastAiBootProgress(ct), ct);
	}

	private void StartRemoteMonitor()
	{
		// 既に動作中なら何もしない
		if (remoteMonitor_ != null && remoteMonitor_.IsMonitoring)
			return;

		string host = Settings.EngineSettings.RemoteHost;
		int sshPort = Settings.EngineSettings.VastAiSshPort;
		string keyPath = Settings.EngineSettings.VastAiSshKeyPath;

		if (string.IsNullOrEmpty(host) || sshPort <= 0 || string.IsNullOrEmpty(keyPath))
			return;

		remoteMonitor_?.Dispose();
		remoteMonitor_ = new ShogiGUI.Engine.RemoteMonitor();
		remoteMonitor_.Updated += (cpu, gpu) =>
		{
			RunOnUiThread(() => infoPageAdepter?.SetRemoteStats(cpu, gpu));
		};
		remoteMonitor_.Start(host, sshPort, keyPath);
	}

	private void StopRemoteMonitor()
	{
		remoteMonitor_?.Dispose();
		remoteMonitor_ = null;
		RunOnUiThread(() => infoPageAdepter?.HideRemoteStats());
	}

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
			ShowJointBoardDialog(sNotation, allowAddToNotation: true);
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
				ShowJointBoardDialog(sNotation, allowAddToNotation: true);
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
		analyzeStartDialog.ParallelClick = (EventHandler<EventArgs>)Delegate.Combine(analyzeStartDialog.ParallelClick, (EventHandler<EventArgs>)delegate
		{
			ShowParallelSettingsDialog();
		});
		analyzeStartDialog.Show(FragmentManager, "AnalyzeStartDialog");
	}

	private void ShowParallelSettingsDialog()
	{
		var layout = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
		layout.SetPadding(48, 32, 48, 16);

		var title = new TextView(this) { Text = "並列解析設定" };
		title.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
		title.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
		layout.AddView(title);

		var workersEdit = AddSettingRow(layout, "並列数", Settings.AnalyzeSettings.ParallelWorkers.ToString());
		var nodesEdit = AddSettingRow(layout, "ノード数(百万)", Settings.AnalyzeSettings.ParallelNodesMillions.ToString());
		var threadsEdit = AddSettingRow(layout, "スレッド/ワーカー", Settings.AnalyzeSettings.ParallelThreadsPerWorker.ToString());
		var hashEdit = AddSettingRow(layout, "Hash/ワーカー(MB)", Settings.AnalyzeSettings.ParallelHashPerWorker.ToString());

		new AlertDialog.Builder(this)
			.SetView(layout)
			.SetPositiveButton("解析開始", (s, e) =>
			{
				if (int.TryParse(workersEdit.Text, out int w) && w > 0)
					Settings.AnalyzeSettings.ParallelWorkers = w;
				if (int.TryParse(nodesEdit.Text, out int n) && n > 0)
					Settings.AnalyzeSettings.ParallelNodesMillions = n;
				if (int.TryParse(threadsEdit.Text, out int t) && t > 0)
					Settings.AnalyzeSettings.ParallelThreadsPerWorker = t;
				if (int.TryParse(hashEdit.Text, out int h) && h > 0)
					Settings.AnalyzeSettings.ParallelHashPerWorker = h;
				Settings.Save();
				RunParallelAnalysis();
			})
			.SetNegativeButton("キャンセル", (s, e) => { })
			.Show();
	}

	private CancellationTokenSource parallelAnalyzeCts_;
	private AlertDialog parallelProgressDialog_;
	private TextView parallelProgressText_;
	private ProgressBar parallelProgressBar_;

	private EditText AddSettingRow(LinearLayout parent, string label, string value)
	{
		var row = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
		var lp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		lp.TopMargin = 8;
		row.LayoutParameters = lp;

		var tv = new TextView(this) { Text = label };
		tv.LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		row.AddView(tv);

		var et = new EditText(this) { Text = value };
		et.InputType = Android.Text.InputTypes.ClassNumber;
		et.LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1f);
		row.AddView(et);

		parent.AddView(row);
		return et;
	}

	private async void RunParallelAnalysis()
	{
		int workers = Settings.AnalyzeSettings.ParallelWorkers;
		long nodesPerMove = (long)Settings.AnalyzeSettings.ParallelNodesMillions * 1000000L;

		parallelAnalyzeCts_ = new CancellationTokenSource();
		var game = ShogiGUI.Domain.Game;

		ShowParallelProgressDialog();

		game.ParallelAnalyzeProgress += OnParallelProgress;

		try
		{
			await game.ParallelAnalyzeAsync(workers, nodesPerMove, parallelAnalyzeCts_.Token);
			RunOnUiThread(() =>
			{
				DismissParallelProgressDialog();
				Toast.MakeText(this, "並列解析完了", ToastLength.Short).Show();
				UpdateNotation(ShogiGUI.Events.NotationEventId.OBJECT_CHANGED);
			});
		}
		catch (System.OperationCanceledException)
		{
			RunOnUiThread(() =>
			{
				DismissParallelProgressDialog();
				Toast.MakeText(this, "解析キャンセル", ToastLength.Short).Show();
			});
		}
		catch (Exception ex)
		{
			RunOnUiThread(() =>
			{
				DismissParallelProgressDialog();
				Toast.MakeText(this, $"解析エラー: {ex.Message}", ToastLength.Long).Show();
			});
		}
		finally
		{
			game.ParallelAnalyzeProgress -= OnParallelProgress;
			parallelAnalyzeCts_ = null;
		}
	}

	private void ShowParallelProgressDialog()
	{
		var layout = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
		layout.SetPadding(48, 32, 48, 16);

		parallelProgressText_ = new TextView(this) { Text = "並列解析を準備中..." };
		parallelProgressText_.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
		layout.AddView(parallelProgressText_);

		parallelProgressBar_ = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal);
		parallelProgressBar_.Indeterminate = true;
		var barLp = new LinearLayout.LayoutParams(
			LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
		barLp.TopMargin = 16;
		parallelProgressBar_.LayoutParameters = barLp;
		layout.AddView(parallelProgressBar_);

		parallelProgressDialog_ = new AlertDialog.Builder(this)
			.SetTitle("並列解析")
			.SetView(layout)
			.SetNegativeButton("キャンセル", (s, e) =>
			{
				parallelAnalyzeCts_?.Cancel();
			})
			.SetCancelable(false)
			.Create();
		parallelProgressDialog_.Show();
	}

	private void DismissParallelProgressDialog()
	{
		try { parallelProgressDialog_?.Dismiss(); } catch { }
		parallelProgressDialog_ = null;
		parallelProgressText_ = null;
		parallelProgressBar_ = null;
	}

	private void OnParallelProgress(string msg)
	{
		RunOnUiThread(() =>
		{
			if (parallelProgressText_ != null)
				parallelProgressText_.Text = msg;

			if (parallelProgressBar_ != null)
			{
				var m = System.Text.RegularExpressions.Regex.Match(msg, @"(\d+)/(\d+)");
				if (m.Success && int.TryParse(m.Groups[1].Value, out int done)
					&& int.TryParse(m.Groups[2].Value, out int total) && total > 0)
				{
					parallelProgressBar_.Indeterminate = false;
					parallelProgressBar_.Max = total;
					parallelProgressBar_.Progress = done;
				}
			}
		});
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
			bottomTime.SetTextColor(ColorUtils.Get(this, Resource.Color.time_text_color));
			topTime.SetTextColor(new Color(defaultTimeTextColor));
		}
		else
		{
			topTime.SetTextColor(ColorUtils.Get(this, Resource.Color.time_text_color));
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
			UpdateThreatmateBadge();
			return;
		}
		char c = notation.MoveCurrent.Turn.ToChar();
		notationText.Text = $"{notation.MoveCurrent.Number} {c}{notation.MoveCurrent.ToString(Settings.AppSettings.MoveStyle)}";
		UpdateThreatmateBadge();
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
		// Policy ページの表示切替を設定変更時に反映
		bool newPolicyDisp = Settings.AppSettings.AutoPolicyAnalysis;
		if (infoPageAdepter.DispPolicyPage != newPolicyDisp)
		{
			infoPageAdepter.DispPolicyPage = newPolicyDisp;
			infoPageAdepter.NotifyDataSetChanged();
		}
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
			menuButton.SetImageResource(Resource.Drawable.ic_menu_hamburger);
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
			// ステータスバー・ナビゲーションバーを表示
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.Visible;
			Window.ClearFlags(WindowManagerFlags.Fullscreen);
			AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, true);
			var drawer = FindViewById<AndroidX.DrawerLayout.Widget.DrawerLayout>(Resource.Id.drawer_layout);
			if (drawer != null)
			{
				drawer.SetFitsSystemWindows(true);
				drawer.SetStatusBarBackgroundColor(Android.Graphics.Color.Black);
				drawer.RequestFitSystemWindows();
			}
		}
		else
		{
			// ステータスバー・ナビゲーションバーを非表示、アプリを全画面に広げる
			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
				SystemUiFlags.ImmersiveSticky |
				SystemUiFlags.HideNavigation |
				SystemUiFlags.Fullscreen |
				SystemUiFlags.LayoutStable |
				SystemUiFlags.LayoutHideNavigation |
				SystemUiFlags.LayoutFullscreen);
			Window.Attributes.Flags |= WindowManagerFlags.Fullscreen;
			// ステータスバー・ナビバーを完全透明にしてウィンドウ背景を透過
			Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
			Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
			// ノッチ・カットアウト領域にもコンテンツを描画
			if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.P)
			{
				Window.Attributes.LayoutInDisplayCutoutMode =
					Android.Views.LayoutInDisplayCutoutMode.ShortEdges;
			}
			AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, false);
			var drawer = FindViewById<AndroidX.DrawerLayout.Widget.DrawerLayout>(Resource.Id.drawer_layout);
			if (drawer != null)
			{
				drawer.SetFitsSystemWindows(false);
				drawer.SetStatusBarBackgroundColor(Android.Graphics.Color.Transparent);
				drawer.RequestFitSystemWindows();
			}
		}

		UpdateTopShortcutVisibility();
	}

	private void UpdateTopShortcutVisibility()
	{
		if (topShortcutBar == null)
		{
			return;
		}

		bool isPortrait = Resources?.Configuration?.Orientation != Orientation.Landscape;
		if (!isPortrait || !Settings.AppSettings.DispToolbar)
		{
			topShortcutBar.Visibility = ViewStates.Visible;
			if (topShortcutButtons != null)
			{
				topShortcutButtons.Visibility = ViewStates.Visible;
			}
			return;
		}

		bool showCloseButton = bookBrowseCloseButton?.Visibility == ViewStates.Visible;
		if (topShortcutButtons != null)
		{
			topShortcutButtons.Visibility = ViewStates.Gone;
		}
		topShortcutBar.Visibility = showCloseButton ? ViewStates.Visible : ViewStates.Gone;
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

#if DEBUG
	private BroadcastReceiver debugReceiver;

	private void RegisterDebugReceiver()
	{
		if (debugReceiver != null) return;
		debugReceiver = new DebugCommandReceiver(this);
		var filter = new IntentFilter("com.ngs43.shogidroid.DEBUG");
		if ((int)Build.VERSION.SdkInt >= 33)
			RegisterReceiver(debugReceiver, filter, (ActivityFlags)0x2 /* RECEIVER_EXPORTED */);
		else
			RegisterReceiver(debugReceiver, filter);
	}

	private void UnregisterDebugReceiver()
	{
		if (debugReceiver != null)
		{
			UnregisterReceiver(debugReceiver);
			debugReceiver = null;
		}
	}

	private void DispatchDebugCommand(string cmd, Intent intent)
	{
		switch (cmd)
		{
		case "analyze":
			AnalyzeButton_Click(this, EventArgs.Empty);
			break;
		case "next":
			presenter.Next();
			break;
		case "prev":
			presenter.Prev();
			break;
		case "first":
			presenter.First();
			break;
		case "last":
			presenter.Last();
			break;
		case "reverse":
			MenuExceute(CmdNo.Reverse);
			break;
		case "menu":
			MenuButton_Click(this, EventArgs.Empty);
			break;
		case "stop":
			presenter.Stop();
			break;
		case "cancel":
			presenter.InputCancel();
			break;
		case "screenshot":
			TakeDebugScreenshot();
			break;
		case "book_load":
			{
				string bookPath = intent.GetStringExtra("path");
				string mode = intent.GetStringExtra("mode");
				bool browse = mode == "browse";
				if (!string.IsNullOrEmpty(bookPath))
				{
					LoadBookAsync(bookPath, browse);
				}
				else
				{
					AppDebug.Log.Warning("DebugReceiver: book_load requires --es path <file>");
				}
			}
			break;
		default:
			AppDebug.Log.Warning($"DebugReceiver: unknown cmd '{cmd}'");
			break;
		}
	}

	private void TakeDebugScreenshot()
	{
		try
		{
			var rootView = Window.DecorView.RootView;
			rootView.DrawingCacheEnabled = true;
			var bmp = Bitmap.CreateBitmap(rootView.DrawingCache);
			rootView.DrawingCacheEnabled = false;
			string path = System.IO.Path.Combine(
				Android.OS.Environment.GetExternalStoragePublicDirectory(
					Android.OS.Environment.DirectoryPictures).AbsolutePath,
				"ShogiDroid", "debug_screenshot.png");
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
			using var fs = new System.IO.FileStream(path, System.IO.FileMode.Create);
			bmp.Compress(Bitmap.CompressFormat.Png, 100, fs);
			bmp.Dispose();
			AppDebug.Log.Info($"Screenshot saved: {path}");
		}
		catch (Exception ex)
		{
			AppDebug.Log.Warning($"Screenshot failed: {ex.Message}");
		}
	}

	private class DebugCommandReceiver : BroadcastReceiver
	{
		private readonly MainActivity activity;
		public DebugCommandReceiver(MainActivity a) { activity = a; }

		public override void OnReceive(Context context, Intent intent)
		{
			string cmd = intent.GetStringExtra("cmd");
			if (string.IsNullOrEmpty(cmd)) return;
			AppDebug.Log.Info($"DebugReceiver: cmd={cmd}");
			activity.RunOnUiThread(() => activity.DispatchDebugCommand(cmd, intent));
		}
	}
#endif
}
