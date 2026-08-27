using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace SeddikClinic.Patient.App.Platforms.Android;

public static class AndroidNotificationManager
{
    private const string ChannelId = "seddik_patient_channel";
    private const string ChannelName = "إشعارات مواعيد عيادة صديق";

    public static void Initialize(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.High)
            {
                Description = "تنبيهات حالة الحجز ومواعيد الكشف"
            };
            channel.EnableVibration(true);
            channel.EnableLights(true);
            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
        }
    }

    public static void SendNotification(string title, string message)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            Initialize(context);

            var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
            PendingIntent? pendingIntent = null;
            if (intent != null)
            {
                intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
                pendingIntent = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            }

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetAutoCancel(true)
                .SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate));

            if (pendingIntent != null)
            {
                builder.SetContentIntent(pendingIntent);
            }

            var notificationManager = NotificationManagerCompat.From(context);
            var notificationId = new System.Random().Next(1000, 99999);
            notificationManager.Notify(notificationId, builder.Build());
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Patient Notification Error]: {ex.Message}");
        }
    }
}
