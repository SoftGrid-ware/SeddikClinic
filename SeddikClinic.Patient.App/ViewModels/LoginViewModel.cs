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
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _serverUrl = ApiConfig.BaseUrl;

    [ObservableProperty]
    private bool _showServerConfig;

    [ObservableProperty]
    private bool _isNewPatient;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public LoginViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
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
                ErrorMessage = $"تعذر الاتصال بالسيرفر ({ServerUrl}). تأكد من أن جهاز الكمبيوتر متصل بنفس شبكة الواي فاي والسيرفر قيد التشغيل.";
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
            ErrorMessage = "يرجى إدخال رقم الهاتف";
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

            if (!IsNewPatient)
            {
                var patient = await _apiClient.GetPatientByPhoneAsync(PhoneNumber.Trim());
                if (patient != null)
                {
                    SetPatientSession(patient);
                    await Shell.Current.GoToAsync("//HomePage");
                    return;
                }
                else
                {
                    // If no connection, report error
                    if (!string.IsNullOrEmpty(_apiClient.LastErrorMessage))
                    {
                        var isConnected = await _apiClient.CheckConnectionAsync();
                        if (!isConnected)
                        {
                            ErrorMessage = $"تعذر الاتصال بسيرفر العيادة ({_apiClient.BaseUrl}).\nيرجى التأكد من اتصال الهاتف بالواي فاي والضغط على 'إعدادات السيرفر'.";
                            ShowServerConfig = true;
                            return;
                        }
                    }

                    IsNewPatient = true;
                    ErrorMessage = "لم يتم العثور على حساب مسجل بهذا الرقم. يرجى إدخال اسمك الكريم لإنشاء حساب جديد.";
                    return;
                }
            }

            // تسجيل مريض جديد
            if (string.IsNullOrWhiteSpace(FullName))
            {
                ErrorMessage = "يرجى إدخال الاسم بالكامل";
                return;
            }

            var newPatientDto = new CreatePatientDto
            {
                FullName = FullName.Trim(),
                PhoneNumber = PhoneNumber.Trim()
            };

            var created = await _apiClient.CreatePatientAsync(newPatientDto);
            if (created != null)
            {
                SetPatientSession(created);
                await Shell.Current.GoToAsync("//HomePage");
            }
            else
            {
                var detail = _apiClient.LastErrorMessage ?? "يرجى التأكد من تشغيل السيرفر ورابط الاتصال.";
                ErrorMessage = $"تعذر إنشاء الحساب:\n{detail}";
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
