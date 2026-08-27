using SeddikClinic.Mobile.Shared.Services;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App.Helpers;

public static class ServiceHelper
{
    public static IServiceProvider? Services => 
        Application.Current?.Handler?.MauiContext?.Services 
        ?? IPlatformApplication.Current?.Services;

    private static MobileApiClient? _fallbackApiClient;
    public static MobileApiClient FallbackApiClient => _fallbackApiClient ??= new MobileApiClient();

    public static T GetService<T>() where T : class
    {
        try
        {
            var service = Services?.GetService<T>();
            if (service != null) return service;
        }
        catch { }

        try
        {
            if (typeof(T) == typeof(MobileApiClient))
                return (T)(object)FallbackApiClient;
            if (typeof(T) == typeof(HomeViewModel))
                return (T)(object)new HomeViewModel(GetService<MobileApiClient>());
            if (typeof(T) == typeof(BookingViewModel))
                return (T)(object)new BookingViewModel(GetService<MobileApiClient>());
            if (typeof(T) == typeof(MyAppointmentsViewModel))
                return (T)(object)new MyAppointmentsViewModel(GetService<MobileApiClient>());
            if (typeof(T) == typeof(ProfileViewModel))
                return (T)(object)new ProfileViewModel(GetService<MobileApiClient>());
            if (typeof(T) == typeof(LoginViewModel))
                return (T)(object)new LoginViewModel(GetService<MobileApiClient>());
            if (typeof(T) == typeof(ContactUsViewModel))
                return (T)(object)new ContactUsViewModel();

            return Activator.CreateInstance<T>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ServiceHelper Create Instance Error for {typeof(T).Name}]: {ex}");
            throw;
        }
    }
}
