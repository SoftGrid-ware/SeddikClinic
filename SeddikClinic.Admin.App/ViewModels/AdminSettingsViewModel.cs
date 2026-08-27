using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Settings;
using SeddikClinic.Mobile.Shared.Helpers;
using SeddikClinic.Mobile.Shared.Models;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App.ViewModels;

public partial class AdminSettingsViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userRole = string.Empty;

    [ObservableProperty]
    private string _serverUrl = ApiConfig.BaseUrl;

    // Working Hours Controls
    [ObservableProperty]
    private string _startTime = "17:00";

    [ObservableProperty]
    private string _endTime = "22:30";

    [ObservableProperty]
    private int _slotDurationMinutes = 30;

    [ObservableProperty]
    private string _clinicDays = "يومياً عدا الجمعة";

    public string ClinicName => "عيادة د. صديق التخصصية";
    public string DeveloperName => "د. عبدالرحمن شعبان محمد";
    public string DeveloperPhone => "01009563353";

    public AdminSettingsViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async void LoadSettings()
    {
        UserName = AppSession.FullName ?? AppSession.UserName ?? "المدير";
        UserRole = AppSession.Role switch
        {
            SeddikClinic.Core.Enums.UserRole.Manager => "مدير المنظومة",
            SeddikClinic.Core.Enums.UserRole.Doctor => "طبيب",
            SeddikClinic.Core.Enums.UserRole.Assistant => "مساعد / موظف استقبال",
            _ => "مستخدم النظام"
        };
        ServerUrl = _apiClient.BaseUrl;

        try
        {
            var config = await _apiClient.GetWorkingHoursAsync();
            if (config != null)
            {
                StartTime = config.StartTime;
                EndTime = config.EndTime;
                SlotDurationMinutes = config.SlotDurationMinutes;
                ClinicDays = config.ClinicDays;
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task SaveWorkingHoursAsync()
    {
        var dto = new WorkingHoursConfigDto
        {
            StartTime = StartTime.Trim(),
            EndTime = EndTime.Trim(),
            SlotDurationMinutes = SlotDurationMinutes > 0 ? SlotDurationMinutes : 30,
            ClinicDays = ClinicDays.Trim()
        };

        var ok = await _apiClient.UpdateWorkingHoursAsync(dto);
        if (ok)
        {
            await Shell.Current.DisplayAlert("تم الحفظ", "تم تحديث مواعيد العمل بنجاح ومزامنتها مع كافة التطبيقات", "حسناً");
        }
        else
        {
            await Shell.Current.DisplayAlert("خطأ", "تعذر حفظ مواعيد العمل", "حسناً");
        }
    }

    [RelayCommand]
    private async Task SaveServerUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl)) return;
        _apiClient.SetBaseUrl(ServerUrl);
        await Shell.Current.DisplayAlert("تم الحفظ", "تم تحديث رابط السيرفر بنجاح", "حسناً");
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

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await Shell.Current.DisplayAlert("تسجيل الخروج", "هل أنت متأكد من رغبتك في تسجيل الخروج؟", "نعم", "إلغاء");
        if (confirm)
        {
            _apiClient.Logout();
            await Shell.Current.GoToAsync("//AdminLoginPage");
        }
    }
}
