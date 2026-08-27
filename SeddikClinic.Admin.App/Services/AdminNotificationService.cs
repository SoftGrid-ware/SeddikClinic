using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App.Services;

public class AdminNotificationService
{
    private readonly MobileApiClient _apiClient;
    private int _lastAppointmentCount = -1;
    private bool _isPolling;

    public AdminNotificationService(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task InitializeAsync()
    {
        try
        {
#if ANDROID
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
            }
#endif
        }
        catch { }

        StartPolling();
    }

    public void StartPolling()
    {
        if (_isPolling) return;
        _isPolling = true;

        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var summary = await _apiClient.GetTodayAppointmentsSummaryAsync();
                    if (summary != null)
                    {
                        if (_lastAppointmentCount >= 0 && summary.TotalToday > _lastAppointmentCount)
                        {
                            string title = "حجز مريض جديد 🔔";
                            string msg = $"تم تسجيل حجز جديد في العيادة! إجمالي حجوزات اليوم: {summary.TotalToday} مريض";

#if ANDROID
                            Platforms.Android.AndroidNotificationManager.SendNotification(title, msg);
#endif
                            try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
                        }
                        _lastAppointmentCount = summary.TotalToday;
                    }
                }
                catch { }

                await Task.Delay(8000);
            }
        });
    }
}
