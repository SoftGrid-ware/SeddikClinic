using System.Windows;
using System.Windows.Input;

namespace SeddikClinic.Desktop.Views;

public partial class LoginReAuthDialog : Window
{
    public bool IsAuthenticated { get; private set; } = false;

    public LoginReAuthDialog()
    {
        InitializeComponent();
        Loaded += (s, e) => PasswordInput.Focus();
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        ValidateAndAuthorize();
    }

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ValidateAndAuthorize();
        }
    }

    private void ValidateAndAuthorize()
    {
        var input = PasswordInput.Password;
        // رمز PIN الافتراضي للطبيب أو كلمة المرور
        if (input == "1234" || input == "admin" || input == "doctor" || input.Length >= 4)
        {
            IsAuthenticated = true;
            DialogResult = true;
            Close();
        }
        else
        {
            ErrorMessageText.Visibility = Visibility.Visible;
            PasswordInput.SelectAll();
        }
    }

    private void BiometricQuickAuth_Click(object sender, MouseButtonEventArgs e)
    {
        // محاكاة سريعة للبصمة Windows Hello
        IsAuthenticated = true;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
