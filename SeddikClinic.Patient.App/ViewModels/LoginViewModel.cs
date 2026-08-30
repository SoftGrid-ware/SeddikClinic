using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Mobile.Shared.Helpers;
using SeddikClinic.Mobile.Shared.Models;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Patient.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isPasswordHidden = true;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isRegisterMode;

    [ObservableProperty]
    private bool _isSettingInitialPassword;

    [ObservableProperty]
    private string _initialPassword = string.Empty;

    [ObservableProperty]
    private string _confirmInitialPassword = string.Empty;

    [ObservableProperty]
    private string _serverUrl = ApiConfig.BaseUrl;

    [ObservableProperty]
    private bool _showServerConfig;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    private PatientDto? _pendingPatient;

    public LoginViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
        LoadSavedCredentials();
    }

    private void LoadSavedCredentials()
    {
        try
        {
            var savedPhone = Preferences.Get("SavedPatientPhone", string.Empty);
            if (!string.IsNullOrWhiteSpace(savedPhone))
            {
                PhoneNumber = savedPhone;
            }
        }
        catch { }
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
    }

    [RelayCommand]
    private void SwitchMode()
    {
        IsRegisterMode = !IsRegisterMode;
        ErrorMessage = string.Empty;
        IsSettingInitialPassword = false;
    }

    [RelayCommand]
    private void ToggleServerConfig()
    {
        ShowServerConfig = !ShowServerConfig;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl)) return;
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            _apiClient.SetBaseUrl(ServerUrl);
            var isOk = await _apiClient.CheckConnectionAsync(ServerUrl);
            if (isOk)
            {
                await Shell.Current.DisplayAlert("نجاح الاتصال ✅", $"تم الاتصال بنجاح بسيرفر العيادة:\n{_apiClient.BaseUrl}", "حسناً");
            }
            else
            {
                ErrorMessage = $"تعذر الاتصال بالسيرفر ({ServerUrl}). تأكد من أن السيرفر قيد التشغيل.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"خطأ في الاتصال: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            ErrorMessage = "يرجى إدخال رقم الهاتف.";
            return;
        }

        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            if (!string.IsNullOrWhiteSpace(ServerUrl))
            {
                _apiClient.SetBaseUrl(ServerUrl);
            }

            if (IsRegisterMode)
            {
                // ==============================
                // 📝 إنشاء حساب جديد
                // ==============================
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    ErrorMessage = "يرجى إدخال الاسم بالكامل.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(Password) || Password.Length < 4)
                {
                    ErrorMessage = "يجب أن تكون كلمة المرور 4 أحرف أو أرقام على الأقل.";
                    return;
                }

                if (Password != ConfirmPassword)
                {
                    ErrorMessage = "كلمة المرور وتأكيد كلمة المرور غير متطابقين.";
                    return;
                }

                var createDto = new CreatePatientDto
                {
                    FullName = FullName.Trim(),
                    PhoneNumber = PhoneNumber.Trim(),
                    Password = Password.Trim()
                };

                var res = await _apiClient.PatientRegisterAsync(createDto);
                if (res.Success && res.Patient != null)
                {
                    Preferences.Set("SavedPatientPhone", PhoneNumber.Trim());
                    SetPatientSession(res.Patient);
                    await Shell.Current.GoToAsync("//HomePage");
                }
                else
                {
                    ErrorMessage = res.Message ?? "تعذر إنشاء الحساب.";
                }
            }
            else
            {
                // ==============================
                // 🔑 تسجيل الدخول
                // ==============================
                var res = await _apiClient.PatientLoginAsync(PhoneNumber.Trim(), Password);
                if (res.Success)
                {
                    if (res.RequiresPasswordSetup && res.Patient != null)
                    {
                        // المريض مسجل مسبقاً بالعيادة وليس لديه كلمة مرور بعد
                        _pendingPatient = res.Patient;
                        IsSettingInitialPassword = true;
                        ErrorMessage = "أهلاً بك! يرجى إنشاء كلمة مرور خاصة بك لحماية حسابك من الآن فصاعداً 🔒";
                        return;
                    }

                    if (res.Patient != null)
                    {
                        Preferences.Set("SavedPatientPhone", PhoneNumber.Trim());
                        SetPatientSession(res.Patient);
                        await Shell.Current.GoToAsync("//HomePage");
                    }
                }
                else
                {
                    // فحص إذا كان السيرفر غير متاح
                    if (!string.IsNullOrEmpty(_apiClient.LastErrorMessage))
                    {
                        var isConnected = await _apiClient.CheckConnectionAsync();
                        if (!isConnected)
                        {
                            ErrorMessage = $"تعذر الاتصال بسيرفر العيادة ({_apiClient.BaseUrl}).\nيرجى التأكد من تشغيل السيرفر أو الاتصال بالواي فاي.";
                            ShowServerConfig = true;
                            return;
                        }
                    }

                    ErrorMessage = res.Message ?? "رقم الهاتف أو كلمة المرور غير صحيحة.";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ غير متوقع: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveInitialPasswordAsync()
    {
        if (_pendingPatient == null) return;

        if (string.IsNullOrWhiteSpace(InitialPassword) || InitialPassword.Length < 4)
        {
            ErrorMessage = "يجب أن تكون كلمة المرور 4 أرقام أو أحرف على الأقل.";
            return;
        }

        if (InitialPassword != ConfirmInitialPassword)
        {
            ErrorMessage = "كلمة المرور وتأكيد كلمة المرور غير متطابقين.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _apiClient.SetPatientPasswordAsync(_pendingPatient.Id, null, InitialPassword);
            if (result.Success)
            {
                Preferences.Set("SavedPatientPhone", PhoneNumber.Trim());
                SetPatientSession(_pendingPatient);
                await Shell.Current.DisplayAlert("تم بنجاح 🔒", "تم تعيين كلمة المرور بنجاح وحماية حسابك!", "دخول");
                await Shell.Current.GoToAsync("//HomePage");
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"خطأ: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CancelInitialPassword()
    {
        IsSettingInitialPassword = false;
        _pendingPatient = null;
        ErrorMessage = string.Empty;
    }

    private void SetPatientSession(PatientDto p)
    {
        PatientSession.PatientId = p.Id;
        PatientSession.PatientName = p.FullName;
        PatientSession.PhoneNumber = p.PhoneNumber;
        PatientSession.PatientCode = p.PatientCode;
        PatientSession.Age = p.Age;
        PatientSession.BloodGroup = p.BloodGroup;
        PatientSession.Allergies = p.Allergies;
        PatientSession.MedicalHistory = p.MedicalHistory;
    }
}
