using System.Windows;
using System.Windows.Controls;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class LoginView : UserControl
{
    private readonly ClinicApiClient _apiClient;
    public event EventHandler? LoginSuccessful;

    public LoginView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        _apiClient.ServerUrlChanged += (s, url) => UpdateServerStatusPill();

        Loaded += async (s, e) =>
        {
            UpdateServerStatusPill();
            await RefreshServerPingAsync();
        };
    }

    private void UpdateServerStatusPill()
    {
        if (LoginServerText != null)
        {
            LoginServerText.Text = $"🌐 {_apiClient.BaseUrl}";
        }
    }

    private async Task RefreshServerPingAsync()
    {
        try
        {
            var ping = await _apiClient.PingServerAsync();
            if (ping.IsOnline)
            {
                LoginServerDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                LoginServerText.Text = $"🌐 {_apiClient.BaseUrl} (🟢 متصل - {ping.LatencyMs}ms)";
            }
            else
            {
                LoginServerDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                LoginServerText.Text = $"🌐 {_apiClient.BaseUrl} (🔴 غير متصل)";
            }
        }
        catch
        {
            LoginServerDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            LoginServerText.Text = $"🌐 {_apiClient.BaseUrl} (🔴 غير متصل)";
        }
    }

    private void ConfigureServerFromLogin_Click(object sender, RoutedEventArgs e)
    {
        var mainWin = Window.GetWindow(this) as MainWindow;
        mainWin?.OpenServerSettings_Click(sender, e);
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameInput.Text.Trim();
        var password = PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("يرجى إدخال اسم المستخدم وكلمة المرور.");
            return;
        }

        LoginBtn.IsEnabled = false;
        LoginBtn.Content = "جاري التحقق...";
        ErrorBanner.Visibility = Visibility.Collapsed;

        try
        {
            var result = await _apiClient.LoginAsync(username, password);
            if (result.Success)
            {
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ShowError(result.Message ?? "اسم المستخدم أو كلمة المرور غير صحيحة.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"تعذر الاتصال بخادم المنظومة: {ex.Message}");
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            LoginBtn.Content = "تسجيل الدخول 🚀";
        }
    }

    private void FastLoginAdmin_Click(object sender, RoutedEventArgs e)
    {
        UsernameInput.Text = "admin";
        PasswordInput.Password = "admin123";
        LoginBtn_Click(sender, e);
    }

    private void FastLoginAssistant_Click(object sender, RoutedEventArgs e)
    {
        UsernameInput.Text = "assistant";
        PasswordInput.Password = "assistant123";
        LoginBtn_Click(sender, e);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }
}
