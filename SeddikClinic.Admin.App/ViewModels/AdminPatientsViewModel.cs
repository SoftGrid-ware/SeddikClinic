using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App.ViewModels;

public partial class AdminPatientsViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<PatientDto> Patients { get; } = new();

    public AdminPatientsViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    public async Task SearchPatientsAsync()
    {
        IsLoading = true;
        try
        {
            var results = await _apiClient.SearchPatientsAsync(SearchQuery, 1, 50);
            Patients.Clear();
            foreach (var p in results)
            {
                Patients.Add(p);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdminPatients Error]: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task EditPatientMedicalAsync(PatientDto? patient)
    {
        if (patient == null) return;

        string choice = await Shell.Current.DisplayActionSheet(
            $"الملف الطبي: {patient.FullName}",
            "إلغاء",
            null,
            "تعديل السجل المرضي والأمراض المزمنة 🩺",
            "تعديل الحساسية والأدوية ⚠️",
            "تعديل فصيلة الدم 🩸",
            "اتصال هاتف بالمريض 📞");

        if (string.IsNullOrEmpty(choice) || choice == "إلغاء") return;

        if (choice == "تعديل السجل المرضي والأمراض المزمنة 🩺")
        {
            string newMed = await Shell.Current.DisplayPromptAsync(
                "السجل الطبي",
                "أدخل تفاصيل الأمراض المزمنة:",
                "حفظ",
                "تراجع",
                initialValue: patient.MedicalHistory ?? "");

            if (newMed != null)
            {
                patient.MedicalHistory = newMed;
                await SavePatientAsync(patient);
            }
        }
        else if (choice == "تعديل الحساسية والأدوية ⚠️")
        {
            string newAllergy = await Shell.Current.DisplayPromptAsync(
                "سجل الحساسية",
                "أدخل أنواع حساسية الأدوية:",
                "حفظ",
                "تراجع",
                initialValue: patient.Allergies ?? "");

            if (newAllergy != null)
            {
                patient.Allergies = newAllergy;
                await SavePatientAsync(patient);
            }
        }
        else if (choice == "تعديل فصيلة الدم 🩸")
        {
            string newBlood = await Shell.Current.DisplayActionSheet("اختر فصيلة الدم:", "إلغاء", null, "A+", "A-", "B+", "B-", "O+", "O-", "AB+", "AB-");
            if (!string.IsNullOrEmpty(newBlood) && newBlood != "إلغاء")
            {
                patient.BloodGroup = newBlood;
                await SavePatientAsync(patient);
            }
        }
        else if (choice == "اتصال هاتف بالمريض 📞")
        {
            if (!string.IsNullOrWhiteSpace(patient.PhoneNumber) && PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(patient.PhoneNumber);
            }
        }
    }

    private async Task SavePatientAsync(PatientDto patient)
    {
        IsLoading = true;
        try
        {
            var dto = new CreatePatientDto
            {
                FullName = patient.FullName,
                PhoneNumber = patient.PhoneNumber,
                AlternativePhone = patient.AlternativePhone,
                NationalId = patient.NationalId,
                Gender = patient.Gender,
                Age = patient.Age,
                Address = patient.Address,
                BloodGroup = patient.BloodGroup,
                MedicalHistory = patient.MedicalHistory,
                Allergies = patient.Allergies,
                Notes = patient.Notes
            };

            var updated = await _apiClient.UpdatePatientAsync(patient.Id, dto);
            if (updated != null)
            {
                await SearchPatientsAsync();
                await Shell.Current.DisplayAlert("تم الحفظ", "تم تحديث وحفظ بيانات المريض في قاعدة البيانات بنجاح.", "حسناً");
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
}
