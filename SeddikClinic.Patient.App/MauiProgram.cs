using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SeddikClinic.Mobile.Shared.Services;
using SeddikClinic.Patient.App.Pages;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<MobileApiClient>();
        builder.Services.AddSingleton<Services.PatientNotificationService>();

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<BookingViewModel>();
        builder.Services.AddTransient<MyAppointmentsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ContactUsViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<BookingPage>();
        builder.Services.AddTransient<MyAppointmentsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ContactUsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
