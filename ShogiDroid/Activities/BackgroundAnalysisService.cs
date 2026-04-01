using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace ShogiDroid;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public class BackgroundAnalysisService : Service
{
	public const string ActionStart = "com.ngs43.shogidroid.action.START_BACKGROUND_ANALYSIS";

	public const string ActionStop = "com.ngs43.shogidroid.action.STOP_BACKGROUND_ANALYSIS";

	public const string ExtraIsConsider = "extra_is_consider";

	private const string NotificationChannelId = "background_analysis_status";

	private const int NotificationId = 4203;

	public override IBinder OnBind(Intent intent)
	{
		return null;
	}

	public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
	{
		string action = intent?.Action ?? ActionStart;
		if (action == ActionStop)
		{
			StopForeground(true);
			StopSelf();
			return StartCommandResult.NotSticky;
		}

		bool isConsider = intent?.GetBooleanExtra(ExtraIsConsider, false) ?? false;
		CreateNotificationChannel();
		StartForegroundCompat(BuildNotification(isConsider));
		return StartCommandResult.Sticky;
	}

	private Notification BuildNotification(bool isConsider)
	{
		int textResId = isConsider
			? Resource.String.BackgroundConsiderNotificationText_Text
			: Resource.String.BackgroundAnalyzeNotificationText_Text;

		var openIntent = new Intent(this, typeof(MainActivity));
		openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
		var pendingIntent = PendingIntent.GetActivity(
			this,
			2,
			openIntent,
			PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

		return new NotificationCompat.Builder(this, NotificationChannelId)
			.SetSmallIcon(Resource.Drawable.shogidroid_icon)
			.SetContentTitle(GetString(Resource.String.BackgroundAnalyzeNotificationTitle_Text))
			.SetContentText(GetString(textResId))
			.SetStyle(new NotificationCompat.BigTextStyle().BigText(GetString(textResId)))
			.SetContentIntent(pendingIntent)
			.SetOnlyAlertOnce(true)
			.SetOngoing(true)
			.SetSilent(true)
			.SetPriority((int)NotificationPriority.Low)
			.Build();
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
			GetString(Resource.String.BackgroundAnalyzeNotificationChannel_Text),
			NotificationImportance.Low)
		{
			Description = GetString(Resource.String.BackgroundAnalyzeNotificationChannelDescription_Text)
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
}
