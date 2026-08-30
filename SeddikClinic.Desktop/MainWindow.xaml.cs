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
    private FacebookMarketingView? _marketingView;
    private AboutSystemView? _aboutView;

    private readonly DispatcherTimer _notificationTimer = new();
    private int _lastAppointmentCount = -1;

    public MainWindow()
    {
        InitializeComponent();
        
        _apiClient = new ClinicApiClient();
        _apiClient.ServerUrlChanged += (s, url) =>
        {
            if (ConnectedServerUrlText != null) ConnectedServerUrlText.Text = url;
        };

        if (ConnectedServerUrlText != null)
        {
            ConnectedServerUrlText.Text = _apiClient.BaseUrl;
        }

        Loaded += async (s, e) =>
        {
            SidebarBorder.Visibility = Visibility.Collapsed;
            SidebarColumn.Width = new GridLength(0);

            StartupStatusText.Text = "جاري فحص الاتصال بالسيرفر وتجهيز المنظومة...";
            await Task.Delay(400);
            await _apiClient.AutoDetectAndConnectAsync();
            ConnectedServerUrlText.Text = _apiClient.BaseUrl;
            StartupStatusText.Text = "تم بدء التشغيل وتجهيز المنظومة بنجاح";
            await Task.Delay(400);

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
                // فحص دوري لحالة الاتصال الحقيقية بالسيرفر وزمن الاستجابة
                var ping = await _apiClient.PingServerAsync();
                UpdateConnectionStatusUI(ping.IsOnline, ping.LatencyMs);
                
                if (ping.IsOnline)
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
            }
            catch
            {
                UpdateConnectionStatusUI(false);
            }
        };
        _notificationTimer.Start();
    }

    private void UpdateConnectionStatusUI(bool isOnline, long latencyMs = 0)
    {
        if (StatusDot == null || CloudStatusText == null || CloudSubtitleText == null) return;

        if (ConnectedServerUrlText != null)
        {
            ConnectedServerUrlText.Text = _apiClient.BaseUrl;
        }

        if (isOnline)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981 Green
            CloudStatusText.Text = "🟢 أونلاين (متصل)";
            CloudSubtitleText.Text = latencyMs > 0 ? $"متصل ومتزامن (⚡ {latencyMs} ms)" : "العيادة متصلة ومتزامنة لحظياً";
            CloudSubtitleText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #EF4444 Red
            CloudStatusText.Text = "🔴 أوفلاين (غير متصل)";
            CloudSubtitleText.Text = "تعذر الاتصال بالسيرفر";
            CloudSubtitleText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private async void ManualSync_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CloudStatusText.Text = "جاري الفحص... ⏳";
            var ping = await _apiClient.PingServerAsync();
            UpdateConnectionStatusUI(ping.IsOnline, ping.LatencyMs);

            if (ping.IsOnline)
            {
                ClinicMessageBox.Show($"السيرفر متصل ويعمل بكفاءة عالية! ✅\n• الرابط: {_apiClient.BaseUrl}\n• سرعة الاستجابة: {ping.LatencyMs} ms", "نجاح الاتصال", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ClinicMessageBox.Show($"تعذر الاتصال بالسيرفر على الرابط ({_apiClient.BaseUrl}):\n{ping.Message}\n\nيمكنك تعديل رابط السيرفر من زر [⚙️ السيرفر].", "تنبيه الاتصال", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            UpdateConnectionStatusUI(false);
            ClinicMessageBox.Show($"تعذر الاتصال: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================
    // 🌐 إدارة وإعدادات خادم المنظومة (Server Settings Modal)
    // =========================================================

    public async void OpenServerSettings_Click(object sender, RoutedEventArgs e)
    {
        // التحقق من أن المستخدم مدير المنظومة لو كان مسجلاً للدخول
        if (_apiClient.CurrentUser != null && _apiClient.CurrentUser.Role != UserRole.Manager)
        {
            ClinicMessageBox.Show("تعديل إعدادات ورابط السيرفر متاح فقط لمدير المنظومة (Admin).", "صلاحيات غير كافية", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ModalServerUrlInput.Text = _apiClient.BaseUrl;
        ModalDiagnosticBanner.Visibility = Visibility.Collapsed;
        ServerSettingsModal.Visibility = Visibility.Visible;

        await PerformConnectionTestAsync(ModalServerUrlInput.Text.Trim());
    }

    private void CloseServerSettingsModal_Click(object sender, RoutedEventArgs e)
    {
        ServerSettingsModal.Visibility = Visibility.Collapsed;
    }

    private async void PresetLocal5000_Click(object sender, RoutedEventArgs e)
    {
        ModalServerUrlInput.Text = "http://localhost:5000";
        await PerformConnectionTestAsync("http://localhost:5000");
    }

    private async void PresetLocal8080_Click(object sender, RoutedEventArgs e)
    {
        ModalServerUrlInput.Text = "http://localhost:8080";
        await PerformConnectionTestAsync("http://localhost:8080");
    }

    private async void PresetCloud_Click(object sender, RoutedEventArgs e)
    {
        ModalServerUrlInput.Text = "https://seddikclinic-frinw9km.b4a.run";
        await PerformConnectionTestAsync("https://seddikclinic-frinw9km.b4a.run");
    }

    private async void TestCurrentConnection_Click(object sender, RoutedEventArgs e)
    {
        await PerformConnectionTestAsync(ModalServerUrlInput.Text.Trim());
    }

    private async Task PerformConnectionTestAsync(string targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl)) return;

        ModalDiagnosticBanner.Visibility = Visibility.Visible;
        ModalDiagnosticText.Text = $"جاري فحص الاتصال بالرابط: {targetUrl} ... ⏳";
        ModalDiagnosticBanner.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)); // #EFF6FF
        ModalDiagnosticBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(186, 230, 253)); // #BAE6FD
        ModalDiagnosticText.Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)); // #0284C7

        var (isOnline, latencyMs, message) = await _apiClient.PingServerAsync(targetUrl);

        if (isOnline)
        {
            ModalServerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            ModalServerStatusTitle.Text = "🟢 السيرفر متصل وجاهز للعمل (Online)";
            ModalServerStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));
            ModalServerLatencyText.Text = $"⚡ سرعة الاستجابة: {latencyMs} ms • تم التحقق بنجاح من نقطة النهاية /liveness";
            ModalServerStatusCard.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
            ModalServerStatusCard.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208));

            ModalDiagnosticBanner.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
            ModalDiagnosticBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208));
            ModalDiagnosticText.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));
            ModalDiagnosticText.Text = $"✅ الاتصال ناجح تماماً! زمن الاستجابة: {latencyMs} ms";
        }
        else
        {
            ModalServerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            ModalServerStatusTitle.Text = "🔴 تعذر الاتصال بالسيرفر (Offline / Error)";
            ModalServerStatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            ModalServerLatencyText.Text = $"⚠️ {message}";
            ModalServerStatusCard.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
            ModalServerStatusCard.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));

            ModalDiagnosticBanner.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
            ModalDiagnosticBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
            ModalDiagnosticText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            ModalDiagnosticText.Text = $"❌ فشل الاتصال: {message}";
        }
    }

    private async void SaveServerUrl_Click(object sender, RoutedEventArgs e)
    {
        var newUrl = ModalServerUrlInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(newUrl))
        {
            ClinicMessageBox.Show("يرجى إدخال رابط سيرفر صحيح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _apiClient.SaveServerUrl(newUrl);
        var ping = await _apiClient.PingServerAsync(newUrl);
        UpdateConnectionStatusUI(ping.IsOnline, ping.LatencyMs);

        ServerSettingsModal.Visibility = Visibility.Collapsed;

        if (ping.IsOnline)
        {
            ClinicMessageBox.Show($"تم حفظ واعتماد رابط السيرفر بنجاح! ✅\nالرابط النشط: {newUrl}\nحالة الاتصال: متصل (⚡ {ping.LatencyMs} ms)", "تم الحفظ والاتصال", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ClinicMessageBox.Show($"تم حفظ رابط السيرفر ({newUrl}) ولكن تعذر الاتصال به حالياً:\n{ping.Message}", "تنبيه الاتصال", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void AutoDetectServer_Click(object sender, RoutedEventArgs e)
    {
        ModalDiagnosticBanner.Visibility = Visibility.Visible;
        ModalDiagnosticText.Text = "جاري البحث عن سيرفرات نشطة محلياً وسحابياً وتجهيز التشغيل... ⏳";

        var ok = await _apiClient.AutoDetectAndConnectAsync();
        ModalServerUrlInput.Text = _apiClient.BaseUrl;
        await PerformConnectionTestAsync(ModalServerUrlInput.Text.Trim());

        if (ok)
        {
            ClinicMessageBox.Show($"تم اكتشاف السيرفر وتشغيله بنجاح! ✅\nالرابط: {_apiClient.BaseUrl}", "تم بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ClinicMessageBox.Show("تعذر الاكتشاف التلقائي. يرجى كتابة عنوان الـ IP للسيرفر يدوياً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void ShowLoginScreen()
    {
        SidebarBorder.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);

        _loginView = new LoginView(_apiClient);
        _loginView.LoginSuccessful += OnLoginSuccessful;
        MainContentContainer.Content = _loginView;
    }

    private void OnLoginSuccessful(object? sender, EventArgs e)
    {
        var user = _apiClient.CurrentUser;
        if (user == null) return;

        SidebarBorder.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(280);

        CurrentUserFullNameText.Text = user.FullName;
        CurrentUserRoleBadgeText.Text = user.Role == UserRole.Manager ? "مدير المنظومة 👑" : "طبيب / موظف استقبال 🩺";

        NavAppointmentsBtn.Visibility = user.CanManageAppointments ? Visibility.Visible : Visibility.Collapsed;
        NavPatientsBtn.Visibility = user.CanManagePatients ? Visibility.Visible : Visibility.Collapsed;
        NavDashboardBtn.Visibility = user.CanViewFinancials ? Visibility.Visible : Visibility.Collapsed;
        NavExpensesBtn.Visibility = user.CanManageExpenses ? Visibility.Visible : Visibility.Collapsed;
        NavUsersBtn.Visibility = (user.Role == UserRole.Manager || user.CanManageUsers) ? Visibility.Visible : Visibility.Collapsed;
        NavServicesBtn.Visibility = (user.Role == UserRole.Manager || user.CanManageUsers) ? Visibility.Visible : Visibility.Collapsed;

        StartNotificationMonitoring();

        if (user.CanManageAppointments) NavAppointments_Click(this, new RoutedEventArgs());
        else if (user.CanViewFinancials) NavDashboard_Click(this, new RoutedEventArgs());
        else if (user.CanManagePatients) NavPatients_Click(this, new RoutedEventArgs());
        else NavExpenses_Click(this, new RoutedEventArgs());
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
        NavMarketingBtn.Background = normalBrush;
        NavMarketingBtn.Foreground = normalTextBrush;
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

    private void NavMarketing_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavMarketingBtn);
        if (_marketingView == null) _marketingView = new FacebookMarketingView(_apiClient);
        MainContentContainer.Content = _marketingView;
    }

    private void NavAbout_Click(object sender, RoutedEventArgs e)
    {
        HighlightNavButton(NavAboutBtn);
        if (_aboutView == null) _aboutView = new AboutSystemView();
        MainContentContainer.Content = _aboutView;
    }
}
