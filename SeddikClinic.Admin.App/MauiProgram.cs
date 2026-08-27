using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SeddikClinic.Admin.App.Pages;
using SeddikClinic.Admin.App.ViewModels;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App;

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
        builder.Services.AddSingleton<Services.AdminNotificationService>();

        // ViewModels
        builder.Services.AddTransient<AdminLoginViewModel>();
        builder.Services.AddTransient<AdminDashboardViewModel>();
        builder.Services.AddTransient<AdminAppointmentsViewModel>();
        builder.Services.AddTransient<AdminPatientsViewModel>();
        builder.Services.AddTransient<AdminExpensesViewModel>();
        builder.Services.AddTransient<AdminSettingsViewModel>();

        // Pages
        builder.Services.AddTransient<AdminLoginPage>();
        builder.Services.AddTransient<AdminDashboardPage>();
        builder.Services.AddTransient<AdminAppointmentsPage>();
        builder.Services.AddTransient<AdminPatientsPage>();
        builder.Services.AddTransient<AdminExpensesPage>();
        builder.Services.AddTransient<AdminSettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
