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
