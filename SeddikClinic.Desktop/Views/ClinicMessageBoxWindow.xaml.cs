using System.Windows;
using System.Windows.Media;

namespace SeddikClinic.Desktop.Views;

public partial class ClinicMessageBoxWindow : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ClinicMessageBoxWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        InitializeComponent();

        TitleText.Text = string.IsNullOrWhiteSpace(title) ? "تنبيه" : title;
        MessageContentText.Text = message;

        ConfigureAppearance(image);
        ConfigureButtons(buttons);
    }

    private void ConfigureAppearance(MessageBoxImage image)
    {
        switch (image)
        {
            case MessageBoxImage.Information:
                // فحص إذا كانت الرسالة نجاح
                if (TitleText.Text.Contains("نجاح") || TitleText.Text.Contains("تم") || TitleText.Text.Contains("إضافة") || TitleText.Text.Contains("حفظ"))
                {
                    IconText.Text = "✅";
                    IconBadge.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // #F0FDF4
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)); // #BBF7D0
                    BtnOk.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981
                }
                else
                {
                    IconText.Text = "✨";
                    IconBadge.Background = new SolidColorBrush(Color.FromRgb(224, 242, 254)); // #E0F2FE
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(186, 230, 253)); // #BAE6FD
                    BtnOk.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)); // #0284C7
                }
                break;

            case MessageBoxImage.Warning:
                IconText.Text = "⚠️";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // #FEF3C7
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(253, 230, 138)); // #FDE68A
                BtnOk.Background = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // #D97706
                BtnYes.Background = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                break;

            case MessageBoxImage.Error:
                IconText.Text = "❌";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #FEF2F2
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202)); // #FECACA
                BtnOk.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
                break;

            case MessageBoxImage.Question:
                IconText.Text = "❓";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(245, 243, 255)); // #F5F3FF
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(221, 214, 254)); // #DDD6FE
                BtnYes.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)); // #0284C7
                break;

            default:
                IconText.Text = "🔔";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                BtnOk.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                break;
        }
    }

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        BtnOk.Visibility = Visibility.Collapsed;
        BtnYes.Visibility = Visibility.Collapsed;
        BtnNo.Visibility = Visibility.Collapsed;
        BtnCancel.Visibility = Visibility.Collapsed;

        switch (buttons)
        {
            case MessageBoxButton.OK:
                BtnOk.Visibility = Visibility.Visible;
                Result = MessageBoxResult.OK;
                break;

            case MessageBoxButton.OKCancel:
                BtnOk.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                Result = MessageBoxResult.Cancel;
                break;

            case MessageBoxButton.YesNo:
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                Result = MessageBoxResult.No;
                break;

            case MessageBoxButton.YesNoCancel:
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                Result = MessageBoxResult.Cancel;
                break;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        Close();
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Yes;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public static class ClinicMessageBox
{
    public static MessageBoxResult Show(string message, string title = "تنبيه", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            return Application.Current.Dispatcher.Invoke(() => Show(message, title, buttons, image));
        }

        var dlg = new ClinicMessageBoxWindow(message, title, buttons, image);
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dlg.Owner = Application.Current.MainWindow;
        }

        dlg.ShowDialog();
        return dlg.Result;
    }

    public static MessageBoxResult Show(string message)
    {
        return Show(message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static MessageBoxResult Show(string message, string title)
    {
        return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static MessageBoxResult Show(string message, string title, MessageBoxButton button)
    {
        return Show(message, title, button, MessageBoxImage.Information);
    }
}
