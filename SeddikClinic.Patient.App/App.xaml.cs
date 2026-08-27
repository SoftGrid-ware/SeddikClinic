namespace SeddikClinic.Patient.App;

public partial class App : Application
{
    private readonly Services.PatientNotificationService? _notifService;

    public App(Services.PatientNotificationService notifService)
    {
        InitializeComponent();
        _notifService = notifService;
        _ = _notifService.InitializeAsync();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[PATIENT UNHANDLED EXCEPTION]: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[PATIENT UNOBSERVED TASK EXCEPTION]: {e.Exception}");
            e.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
