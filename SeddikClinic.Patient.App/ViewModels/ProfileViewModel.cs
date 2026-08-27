using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Mobile.Shared.Models;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Patient.App.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private string _patientName = string.Empty;

    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    [ObservableProperty]
    private string _patientCode = string.Empty;

    [ObservableProperty]
    private string _bloodGroup = "غير محدد";

    [ObservableProperty]
    private string _allergies = "لا توجد حساسية مسجلة";

    [ObservableProperty]
    private string _medicalHistory = "لا يوجد تاريخ مرضي مسجل";

    [ObservableProperty]
    private bool _isLoading;

    public ProfileViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public void LoadProfile()
    {
        PatientName = PatientSession.PatientName ?? "-";
        PhoneNumber = PatientSession.PhoneNumber ?? "-";
        PatientCode = PatientSession.PatientCode ?? "-";
        BloodGroup = string.IsNullOrEmpty(PatientSession.BloodGroup) ? "غير محدد" : PatientSession.BloodGroup;
        Allergies = string.IsNullOrEmpty(PatientSession.Allergies) ? "لا توجد حساسية مسجلة" : PatientSession.Allergies;
        MedicalHistory = string.IsNullOrEmpty(PatientSession.MedicalHistory) ? "لا يوجد تاريخ مرضي مسجل" : PatientSession.MedicalHistory;
    }

    [RelayCommand]
    private async Task EditMedicalInfoAsync()
    {
        if (!PatientSession.PatientId.HasValue) return;

        string choice = await Shell.Current.DisplayActionSheet(
            "تعديل وتحديث السجل الصحي",
            "إلغاء",
            null,
            "تحديث الأمراض المزمنة والتاريخ الطبي 🩺",
            "تحديث سجل الحساسية والأدوية ⚠️",
            "تحديث فصيلة الدم 🩸");

        if (string.IsNullOrEmpty(choice) || choice == "إلغاء") return;

        if (choice == "تحديث الأمراض المزمنة والتاريخ الطبي 🩺")
        {
            string newHistory = await Shell.Current.DisplayPromptAsync(
                "السجل الطبي والأمراض المزمنة",
                "أدخل تفاصيل الأمراض المزمنة (سكري، ضغط، قلب، إلخ):",
                "حفظ",
                "تراجع",
                initialValue: MedicalHistory != "لا يوجد تاريخ مرضي مسجل" ? MedicalHistory : "");

            if (newHistory == null) return;

            await SavePatientUpdatesAsync(medicalHistory: newHistory);
        }
        else if (choice == "تحديث سجل الحساسية والأدوية ⚠️")
        {
            string newAllergies = await Shell.Current.DisplayPromptAsync(
                "سجل الحساسية",
                "أدخل أنواع حساسية الأدوية (بنسلين، بنج، مسكنات، إلخ):",
                "حفظ",
                "تراجع",
                initialValue: Allergies != "لا توجد حساسية مسجلة" ? Allergies : "");

            if (newAllergies == null) return;

            await SavePatientUpdatesAsync(allergies: newAllergies);
        }
        else if (choice == "تحديث فصيلة الدم 🩸")
        {
            string newBlood = await Shell.Current.DisplayActionSheet(
                "اختر فصيلة الدم:",
                "إلغاء",
                null,
                "A+", "A-", "B+", "B-", "O+", "O-", "AB+", "AB-");

            if (string.IsNullOrEmpty(newBlood) || newBlood == "إلغاء") return;

            await SavePatientUpdatesAsync(bloodGroup: newBlood);
        }
    }

    private async Task SavePatientUpdatesAsync(string? medicalHistory = null, string? allergies = null, string? bloodGroup = null)
    {
        if (!PatientSession.PatientId.HasValue) return;

        IsLoading = true;
        try
        {
            var dto = new CreatePatientDto
            {
                FullName = PatientSession.PatientName ?? PatientName,
                PhoneNumber = PatientSession.PhoneNumber ?? PhoneNumber,
                BloodGroup = bloodGroup ?? PatientSession.BloodGroup ?? BloodGroup,
                Allergies = allergies ?? PatientSession.Allergies ?? Allergies,
                MedicalHistory = medicalHistory ?? PatientSession.MedicalHistory ?? MedicalHistory,
                Age = PatientSession.Age
            };

            var updated = await _apiClient.UpdatePatientAsync(PatientSession.PatientId.Value, dto);
            if (updated != null)
            {
                PatientSession.BloodGroup = updated.BloodGroup;
                PatientSession.Allergies = updated.Allergies;
                PatientSession.MedicalHistory = updated.MedicalHistory;

                LoadProfile();
                await Shell.Current.DisplayAlert("تم الحفظ", "تم تحديث السجل الطبي بنجاح وحفظه في سجلات العيادة.", "حسناً");
            }
            else
            {
                await Shell.Current.DisplayAlert("خطأ", "تعذر حفظ التعديلات", "حسناً");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("خطأ", $"حدث خطأ: {ex.Message}", "حسناً");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert("تسجيل الخروج", "هل أنت متأكد من تسجيل الخروج؟", "نعم", "إلغاء");
        if (confirm)
        {
            _apiClient.Logout();
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
