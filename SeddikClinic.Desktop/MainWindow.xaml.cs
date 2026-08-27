using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SeddikClinic.Core.Enums;
using SeddikClinic.Desktop.Services;
using SeddikClinic.Desktop.Views;

namespace SeddikClinic.Desktop;

public partial class MainWindow : Window
{
    private readonly ClinicApiClient _apiClient;
    private LoginView? _loginView;
    private AppointmentsScheduleView? _appointmentsView;
    private PatientDirectoryView? _patientsView;
    private FinancialDashboardView? _dashboardView;
    private ExpenseManagementView? _expenseView;
    private UserManagementView? _userManagementView;
    private ClinicServicesManagementView? _servicesView;
    private InvoicesBillingView? _invoicesView;
    private AboutSystemView? _aboutView;

    private readonly DispatcherTimer _notificationTimer = new();
    private int _lastAppointmentCount = -1;

    public MainWindow()
    {
        InitializeComponent();
        
        _apiClient = new ClinicApiClient();

        Loaded += async (s, e) =>
        {
            SidebarBorder.Visibility = Visibility.Collapsed;
            SidebarColumn.Width = new GridLength(0);

            StartupStatusText.Text = "جاري بدء التشغيل وتجهيز المنظومة...";
            await Task.Delay(600);
            await _apiClient.AutoDetectAndConnectAsync();
            StartupStatusText.Text = "تم بدء التشغيل وتجهيز المنظومة بنجاح";
            await Task.Delay(600);

            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(0.4));
            fadeAnim.Completed += (sender, args) =>
            {
                StartupLoadingOverlay.Visibility = Visibility.Collapsed;
                ShowLoginScreen();
            };
            StartupLoadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        };
    }

    private void StartNotificationMonitoring()
    {
        _notificationTimer.Interval = TimeSpan.FromSeconds(6);
        _notificationTimer.Tick += async (s, e) =>
        {
            try
            {
                var summary = await _apiClient.GetTodayAppointmentsSummaryAsync();
                if (summary != null)
                {
                    if (_lastAppointmentCount >= 0 && summary.TotalScheduledToday > _lastAppointmentCount)
                    {
                        SystemSounds.Asterisk.Play();
                    }
                    _lastAppointmentCount = summary.TotalScheduledToday;
                }
            }
            catch { }
        };
        _notificationTimer.Start();
    }

    private void ShowLoginScreen()
    {
        SidebarBorder.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);

        _loginView = new LoginView(_apiClient);
        _loginView.LoginSuccessful += (s, e) => OnLoginSuccess();
        MainContentContainer.Content = _loginView;
    }

    private void OnLoginSuccess()
    {
        var user = _apiClient.CurrentUser;
        if (user == null) return;

        // إظهار القائمة الجانبية وتحديث بيانات المستخدم
        SidebarBorder.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(280);

        CurrentUserFullNameText.Text = user.FullName;
        CurrentUserRoleBadgeText.Text = user.RoleBadge;

        // تطبيق مصفوفة الصلاحيات على عناصر القائمة الجانبية
        NavAppointmentsBtn.Visibility = user.CanManageAppointments ? Visibility.Visible : Visibility.Collapsed;
        NavPatientsBtn.Visibility = user.CanManagePatients ? Visibility.Visible : Visibility.Collapsed;
        NavDashboardBtn.Visibility = user.CanViewFinancials ? Visibility.Visible : Visibility.Collapsed;
        NavExpensesBtn.Visibility = user.CanManageExpenses ? Visibility.Visible : Visibility.Collapsed;
        NavUsersBtn.Visibility = (user.Role == UserRole.Manager || user.CanManageUsers) ? Visibility.Visible : Visibility.Collapsed;
        NavServicesBtn.Visibility = (user.Role == UserRole.Manager || user.CanManageUsers) ? Visibility.Visible : Visibility.Collapsed;

        StartNotificationMonitoring();

        // فتح الشاشة الافتراضية المناسبة لصلاحيات المستخدم
        if (user.CanManageAppointments)
        {
            NavAppointments_Click(this, new RoutedEventArgs());
        }
        else if (user.CanViewFinancials)
        {
            NavDashboard_Click(this, new RoutedEventArgs());
        }
        else if (user.CanManagePatients)
        {
            NavPatients_Click(this, new RoutedEventArgs());
        }
        else
        {
            NavExpenses_Click(this, new RoutedEventArgs());
        }
    }

    private void LogoutBtn_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show("هل ترغب في تسجيل الخروج من الحساب الحالي؟", "تأكيد الخروج", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            _notificationTimer.Stop();
            _apiClient.Logout();
            ShowLoginScreen();
        }
    }

    private void ResetNavButtons()
    {
        var normalBrush = new SolidColorBrush(Colors.Transparent);
        var normalTextBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

        NavAppointmentsBtn.Background = normalBrush;
        NavAppointmentsBtn.Foreground = normalTextBrush;
        NavPatientsBtn.Background = normalBrush;
        NavPatientsBtn.Foreground = normalTextBrush;
        NavDashboardBtn.Background = normalBrush;
        NavDashboardBtn.Foreground = normalTextBrush;
        NavExpensesBtn.Background = normalBrush;
        NavExpensesBtn.Foreground = normalTextBrush;
        NavInvoicesBtn.Background = normalBrush;
        NavInvoicesBtn.Foreground = normalTextBrush;
        NavUsersBtn.Background = normalBrush;
        NavUsersBtn.Foreground = normalTextBrush;
        NavServicesBtn.Background = normalBrush;
        NavServicesBtn.Foreground = normalTextBrush;
        NavAboutBtn.Background = normalBrush;
        NavAboutBtn.Foreground = normalTextBrush;
    }

    private void HighlightNavButton(System.Windows.Controls.Button btn)
    {
        ResetNavButtons();
        btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
        btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
    }

    private void NavAppointments_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavAppointmentsBtn);
        if (_appointmentsView == null) _appointmentsView = new AppointmentsScheduleView(_apiClient);
        MainContentContainer.Content = _appointmentsView;
        _ = _appointmentsView.LoadAppointmentsDataAsync();
    }

    private void NavPatients_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavPatientsBtn);
        if (_patientsView == null) _patientsView = new PatientDirectoryView(_apiClient);
        MainContentContainer.Content = _patientsView;
    }

    private void NavDashboard_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavDashboardBtn);
        if (_dashboardView == null) _dashboardView = new FinancialDashboardView(_apiClient);
        MainContentContainer.Content = _dashboardView;
    }

    private void NavExpenses_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavExpensesBtn);
        if (_expenseView == null) _expenseView = new ExpenseManagementView(_apiClient);
        MainContentContainer.Content = _expenseView;
    }

    private void NavInvoices_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavInvoicesBtn);
        if (_invoicesView == null) _invoicesView = new InvoicesBillingView(_apiClient);
        MainContentContainer.Content = _invoicesView;
    }

    private void NavUsers_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavUsersBtn);
        if (_userManagementView == null) _userManagementView = new UserManagementView(_apiClient);
        MainContentContainer.Content = _userManagementView;
    }

    private void NavServices_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavServicesBtn);
        if (_servicesView == null) _servicesView = new ClinicServicesManagementView(_apiClient);
        MainContentContainer.Content = _servicesView;
    }

    private void NavAbout_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavAboutBtn);
        if (_aboutView == null) _aboutView = new AboutSystemView();
        MainContentContainer.Content = _aboutView;
    }
}
