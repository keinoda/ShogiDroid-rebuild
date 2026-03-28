using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using ShogiGUI;
using ShogiGUI.Engine;
using ShogiGUI.Models;
using ShogiLib;

namespace ShogiDroid;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public class ParallelAnalysisService : Service
{
	public const string ActionStart = "com.siganus.ShogiDroid.rebuild.action.START_PARALLEL_ANALYSIS";

	public const string ActionCancel = "com.siganus.ShogiDroid.rebuild.action.CANCEL_PARALLEL_ANALYSIS";

	public const string ExtraInputPath = "extra_input_path";

	public const string ExtraBaseFileName = "extra_base_file_name";

	public const string ExtraWorkers = "extra_workers";

	public const string ExtraNodesPerMove = "extra_nodes_per_move";

	public const string ExtraThreadsPerWorker = "extra_threads_per_worker";

	public const string ExtraHashPerWorker = "extra_hash_per_worker";

	public const string ExtraResultPath = "extra_result_path";

	private const string NotificationChannelId = "parallel_analysis";

	private const int NotificationId = 4201;

	private const int CompletionNotificationId = 4202;

	private CancellationTokenSource cancellationTokenSource_;

	private static volatile bool isRunning_;

	public static bool IsRunning => isRunning_;

	public override IBinder OnBind(Intent intent)
	{
		return null;
	}

	public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
	{
		string action = intent?.Action ?? ActionStart;
		if (action == ActionCancel)
		{
			cancellationTokenSource_?.Cancel();
			return StartCommandResult.NotSticky;
		}

		if (isRunning_)
		{
			return StartCommandResult.NotSticky;
		}

		string inputPath = intent?.GetStringExtra(ExtraInputPath) ?? string.Empty;
		string baseFileName = intent?.GetStringExtra(ExtraBaseFileName) ?? string.Empty;
		int workers = intent?.GetIntExtra(ExtraWorkers, 0) ?? 0;
		long nodesPerMove = intent?.GetLongExtra(ExtraNodesPerMove, 0L) ?? 0L;
		int threadsPerWorker = intent?.GetIntExtra(ExtraThreadsPerWorker, 0) ?? 0;
		int hashPerWorker = intent?.GetIntExtra(ExtraHashPerWorker, 0) ?? 0;

		if (string.IsNullOrEmpty(inputPath) || workers <= 0 || nodesPerMove <= 0 || threadsPerWorker <= 0 || hashPerWorker <= 0)
		{
			StopSelf();
			return StartCommandResult.NotSticky;
		}

		CreateNotificationChannel();
		StartForegroundCompat(BuildProgressNotification(
			GetString(Resource.String.ParallelAnalyzeNotificationTitle_Text),
			GetString(Resource.String.ParallelAnalyzeNotificationPreparing_Text)));

		cancellationTokenSource_ = new CancellationTokenSource();
		isRunning_ = true;

		_ = Task.Run(async () =>
		{
			await ExecuteAsync(inputPath, baseFileName, workers, nodesPerMove, threadsPerWorker, hashPerWorker);
		});

		return StartCommandResult.NotSticky;
	}

	private async Task ExecuteAsync(string inputPath, string baseFileName, int workers, long nodesPerMove, int threadsPerWorker, int hashPerWorker)
	{
		string outputPath = string.Empty;
		try
		{
			LocalFile.CreateFolders();

			var notationModel = new NotationModel();
			notationModel.Load(inputPath);
			SNotation notation = new SNotation(notationModel.Notation);

			var results = await ParallelAnalysisTaskRunner.ExecuteAsync(
				notation,
				workers,
				nodesPerMove,
				threadsPerWorker,
				hashPerWorker,
				UpdateProgress,
				cancellationTokenSource_.Token);

			ParallelAnalysisTaskRunner.ApplyResults(notation, results, Settings.AppSettings.MoveStyle);
			outputPath = BuildOutputPath(baseFileName);
			SaveNotation(notation, outputPath);
			LocalFile.ScanFile(outputPath);

			ShowCompletionNotification(
				GetString(Resource.String.ParallelAnalyzeNotificationCompleted_Text),
				Path.GetFileName(outputPath),
				outputPath);
		}
		catch (System.OperationCanceledException)
		{
			ShowCompletionNotification(
				GetString(Resource.String.ParallelAnalyzeNotificationCanceled_Text),
				GetString(Resource.String.ParallelAnalyzeNotificationCanceledSummary_Text),
				null);
		}
		catch (Exception ex)
		{
			AppDebug.Log.ErrorException(ex, "ParallelAnalysisService failed");
			ShowCompletionNotification(
				GetString(Resource.String.ParallelAnalyzeNotificationFailed_Text),
				ex.Message,
				null);
		}
		finally
		{
			TryDelete(inputPath);
			isRunning_ = false;
			cancellationTokenSource_?.Dispose();
			cancellationTokenSource_ = null;
			StopForeground(true);
			StopSelf();
		}
	}

	private void UpdateProgress(string message)
	{
		NotificationManagerCompat.From(this).Notify(
			NotificationId,
			BuildProgressNotification(
				GetString(Resource.String.ParallelAnalyzeNotificationTitle_Text),
				message));
	}

	private Notification BuildProgressNotification(string title, string text)
	{
		var openIntent = BuildOpenAppIntent(null);
		var cancelIntent = new Intent(this, typeof(ParallelAnalysisService));
		cancelIntent.SetAction(ActionCancel);
		var cancelPendingIntent = PendingIntent.GetService(
			this,
			1,
			cancelIntent,
			PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

		return new NotificationCompat.Builder(this, NotificationChannelId)
			.SetSmallIcon(Resource.Drawable.shogidroid_icon)
			.SetContentTitle(title)
			.SetContentText(text)
			.SetStyle(new NotificationCompat.BigTextStyle().BigText(text))
			.SetOnlyAlertOnce(true)
			.SetOngoing(true)
			.SetPriority((int)NotificationPriority.Low)
			.SetContentIntent(openIntent)
			.AddAction(Android.Resource.Drawable.IcMenuCloseClearCancel, GetString(Resource.String.ParallelAnalyzeNotificationCancel_Text), cancelPendingIntent)
			.Build();
	}

	private void ShowCompletionNotification(string title, string text, string resultPath)
	{
		NotificationManagerCompat.From(this).Notify(
			CompletionNotificationId,
			new NotificationCompat.Builder(this, NotificationChannelId)
				.SetSmallIcon(Resource.Drawable.shogidroid_icon)
				.SetContentTitle(title)
				.SetContentText(text)
				.SetStyle(new NotificationCompat.BigTextStyle().BigText(text))
				.SetAutoCancel(true)
				.SetPriority((int)NotificationPriority.Default)
				.SetContentIntent(BuildOpenAppIntent(resultPath))
				.Build());
	}

	private PendingIntent BuildOpenAppIntent(string resultPath)
	{
		var openIntent = new Intent(this, typeof(MainActivity));
		openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
		if (!string.IsNullOrEmpty(resultPath))
		{
			openIntent.PutExtra(ExtraResultPath, resultPath);
		}
		return PendingIntent.GetActivity(
			this,
			0,
			openIntent,
			PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
	}

	private void CreateNotificationChannel()
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.O)
		{
			return;
		}

		var manager = (NotificationManager)GetSystemService(NotificationService);
		if (manager.GetNotificationChannel(NotificationChannelId) != null)
		{
			return;
		}

		var channel = new NotificationChannel(
			NotificationChannelId,
			GetString(Resource.String.ParallelAnalyzeNotificationChannel_Text),
			NotificationImportance.Low)
		{
			Description = GetString(Resource.String.ParallelAnalyzeNotificationChannelDescription_Text)
		};
		manager.CreateNotificationChannel(channel);
	}

	private void StartForegroundCompat(Notification notification)
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
		{
			StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
			return;
		}

		StartForeground(NotificationId, notification);
	}

	private static string BuildOutputPath(string baseFileName)
	{
		string stem = Path.GetFileNameWithoutExtension(baseFileName);
		if (string.IsNullOrEmpty(stem))
		{
			stem = $"parallel_{DateTime.Now:yyyyMMdd_HHmmss}";
		}

		string path = Path.Combine(LocalFile.KifPath, $"{stem}_parallel.kif");
		int suffix = 2;
		while (System.IO.File.Exists(path))
		{
			path = Path.Combine(LocalFile.KifPath, $"{stem}_parallel_{suffix}.kif");
			suffix++;
		}
		return path;
	}

	private static void SaveNotation(SNotation notation, string filename)
	{
		var kifu = new Kifu();
		string ext = Path.GetExtension(filename).ToLower();
		Encoding encoding = ext.Length == 0 || ext[ext.Length - 1] != 'u'
			? Encoding.GetEncoding(932)
			: Encoding.UTF8;
		kifu.Save(notation, filename, encoding);
	}

	private static void TryDelete(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return;
		}
		try
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
		catch
		{
		}
	}
}
