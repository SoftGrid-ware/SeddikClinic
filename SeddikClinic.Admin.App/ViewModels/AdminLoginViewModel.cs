using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using SeddikClinic.Mobile.Shared.Helpers;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App.ViewModels;

public partial class AdminLoginViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _serverUrl = ApiConfig.BaseUrl;

    [ObservableProperty]
    private bool _showServerConfig;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private bool _isBiometricSupported;

    [ObservableProperty]
    private bool _hasSavedCredentials;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public AdminLoginViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadSavedCredentialsAsync();
    }

    public async Task LoadSavedCredentialsAsync()
    {
        try
        {
            RememberMe = Preferences.Get("Admin_RememberMe", true);
            if (RememberMe)
            {
                var savedUser = Preferences.Get("Admin_SavedUser", "dr");
                var savedPass = Preferences.Get("Admin_SavedPass", "123");
                if (!string.IsNullOrWhiteSpace(savedUser))
                {
                    Username = savedUser;
                    Password = savedPass;
                    HasSavedCredentials = true;
                }
            }

            var isAvail = await CrossFingerprint.Current.IsAvailableAsync();
            IsBiometricSupported = isAvail;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdminLogin Load Error]: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleServerConfig()
    {
        ShowServerConfig = !ShowServerConfig;
    }

    [RelayCommand]
    private async Task LoginWithBiometricsAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var isAvail = await CrossFingerprint.Current.IsAvailableAsync();
            if (!isAvail)
            {
                ErrorMessage = "مستشعر البصمة غير متاح أو غير مفعل في هذا الهاتف";
                return;
            }

            var request = new AuthenticationRequestConfiguration(
                "تسجيل الدخول بالبصمة",
                "يرجى وضع إصبعك على مستشعر البصمة للدخول لنظام إدارة عيادة د. صديق");

            var result = await CrossFingerprint.Current.AuthenticateAsync(request);
            if (result.Authenticated)
            {
                // Ensure credentials loaded
                if (string.IsNullOrWhiteSpace(Username)) Username = Preferences.Get("Admin_SavedUser", "dr");
                if (string.IsNullOrWhiteSpace(Password)) Password = Preferences.Get("Admin_SavedPass", "123");

                await LoginAsync();
            }
            else
            {
                ErrorMessage = "فشلت المصادقة بالبصمة، يرجى إدخال كلمة المرور";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"خطأ في البصمة: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "يرجى إدخال اسم المستخدم وكلمة المرور";
            return;
        }

        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            if (!string.IsNullOrWhiteSpace(ServerUrl))
            {
                _apiClient.SetBaseUrl(ServerUrl);
                ApiConfig.BaseUrl = ServerUrl;
            }

            var res = await _apiClient.LoginAsync(Username.Trim(), Password);
            if (res.Success)
            {
                if (RememberMe)
                {
                    Preferences.Set("Admin_RememberMe", true);
                    Preferences.Set("Admin_SavedUser", Username.Trim());
                    Preferences.Set("Admin_SavedPass", Password);
                }
                else
                {
                    Preferences.Set("Admin_RememberMe", false);
                    Preferences.Remove("Admin_SavedUser");
                    Preferences.Remove("Admin_SavedPass");
                }

                await Shell.Current.GoToAsync("//DashboardPage");
            }
            else
            {
                ErrorMessage = res.Message ?? "اسم المستخدم أو كلمة المرور غير صحيحة";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"تعذر الاتصال بالسيرفر: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
