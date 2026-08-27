using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SeddikClinic.Patient.App.ViewModels;

public partial class ContactUsViewModel : ObservableObject
{
    public string ClinicName => "عيادة د. صديق التخصصية لطب وجراحة الفم والأسنان";
    public string ClinicPhone => "01126092725";
    public string ClinicFacebookUrl => "https://www.facebook.com/SeddikDentalClinic";
    public string DeveloperName => "د. عبدالرحمن شعبان محمد";
    public string DeveloperPhone => "01009563353";

    [RelayCommand]
    private void CallClinic()
    {
        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(ClinicPhone);
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task OpenClinicWhatsAppAsync()
    {
        try
        {
            var uri = new Uri($"https://wa.me/201126092725");
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.External);
        }
        catch { }
    }

    [RelayCommand]
    private async Task OpenFacebookPageAsync()
    {
        try
        {
            var uri = new Uri(ClinicFacebookUrl);
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.External);
        }
        catch { }
    }

    [RelayCommand]
    private void CallDeveloper()
    {
        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(DeveloperPhone);
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task OpenWhatsAppAsync()
    {
        try
        {
            var uri = new Uri($"https://wa.me/201009563353");
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.External);
        }
        catch { }
    }
}
