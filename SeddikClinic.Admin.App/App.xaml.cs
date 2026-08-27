namespace SeddikClinic.Admin.App;

public partial class App : Application
{
    private readonly Services.AdminNotificationService? _notifService;

    public App(Services.AdminNotificationService notifService)
    {
        InitializeComponent();
        _notifService = notifService;
        _ = _notifService.InitializeAsync();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[ADMIN UNHANDLED EXCEPTION]: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[ADMIN UNOBSERVED TASK EXCEPTION]: {e.Exception}");
            e.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
