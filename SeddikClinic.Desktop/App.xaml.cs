using System.Windows;
using System.Windows.Threading;

namespace SeddikClinic.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; // منع إغلاق البرنامج
        try
        {
            Views.ClinicMessageBox.Show($"تنبيه: {e.Exception.Message}", "تنبيه النظام", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch
        {
            MessageBox.Show($"تنبيه: {e.Exception.Message}", "تنبيه النظام", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            try
            {
                Views.ClinicMessageBox.Show($"خطأ غير متوقع: {ex.Message}", "تنبيه النظام", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch
            {
                MessageBox.Show($"خطأ غير متوقع: {ex.Message}", "تنبيه النظام", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}
