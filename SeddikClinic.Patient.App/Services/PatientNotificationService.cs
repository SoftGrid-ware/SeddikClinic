using SeddikClinic.Core.Enums;
using SeddikClinic.Mobile.Shared.Models;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Patient.App.Services;

public class PatientNotificationService
{
    private readonly MobileApiClient _apiClient;
    private readonly Dictionary<Guid, AppointmentStatus> _knownStatuses = new();
    private bool _isPolling;

    public PatientNotificationService(MobileApiClient apiClient)
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
                    if (PatientSession.IsLoggedIn && PatientSession.PatientId.HasValue)
                    {
                        var appointments = await _apiClient.GetPatientAppointmentsAsync(PatientSession.PatientId.Value);
                        foreach (var apt in appointments)
                        {
                            if (_knownStatuses.TryGetValue(apt.Id, out var prevStatus))
                            {
                                if (prevStatus != apt.Status)
                                {
                                    _knownStatuses[apt.Id] = apt.Status;
                                    string title = "تنبيه موعد - عيادة د. صديق 🩺";
                                    string msg;
                                    if (apt.Status == AppointmentStatus.Cancelled)
                                    {
                                        msg = $"تم إلغاء موعدك: ({apt.ServiceType}). " +
                                              (!string.IsNullOrWhiteSpace(apt.CancellationReason) ? $"السبب: {apt.CancellationReason}" : "يرجى التواصل مع العيادة.");
                                    }
                                    else
                                    {
                                        msg = $"تم تحديث حالة موعدك ({apt.ServiceType}) إلى: {apt.StatusNameAr}";
                                    }

#if ANDROID
                                    Platforms.Android.AndroidNotificationManager.SendNotification(title, msg);
#endif
                                    try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
                                }
                            }
                            else
                            {
                                _knownStatuses[apt.Id] = apt.Status;
                            }
                        }
                    }
                }
                catch { }

                await Task.Delay(10000);
            }
        });
    }
}
