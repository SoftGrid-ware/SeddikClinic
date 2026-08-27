using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SeddikClinic.Desktop.Views;

public partial class AboutSystemView : UserControl
{
    public AboutSystemView()
    {
        InitializeComponent();
    }

    private void CopyPhone_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText("01009563353");
        MessageBox.Show("تم نسخ رقم هاتف المطور بنجاح:\n01009563353", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://wa.me/201009563353") { UseShellExecute = true });
        }
        catch { }
    }
}
