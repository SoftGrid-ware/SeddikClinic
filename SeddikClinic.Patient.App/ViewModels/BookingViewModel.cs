using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.DTOs.Settings;
using SeddikClinic.Mobile.Shared.Models;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Patient.App.ViewModels;

public partial class BookingViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private bool _isStep1 = true;

    [ObservableProperty]
    private bool _isStep2;

    [ObservableProperty]
    private bool _isStep3;

    [ObservableProperty]
    private bool _isStepSuccess;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // Services
    public ObservableCollection<ClinicServiceDto> Services { get; } = new();

    [ObservableProperty]
    private ClinicServiceDto? _selectedService;

    // Date & Time
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    public DateTime MinDate { get; } = DateTime.Today;
    public DateTime MaxDate { get; } = DateTime.Today.AddDays(30);

    public ObservableCollection<string> AvailableSlots { get; } = new();

    [ObservableProperty]
    private string? _selectedTimeSlot;

    // Working Hours Config
    private WorkingHoursConfigDto _workingHours = new();

    // Medical Health Questionnaire
    [ObservableProperty]
    private bool _isHealthy = true;

    [ObservableProperty]
    private bool _hasDiabetes;

    [ObservableProperty]
    private bool _hasHypertension;

    [ObservableProperty]
    private bool _hasHeartOrBloodThinners;

    [ObservableProperty]
    private bool _hasPenicillinAllergy;

    [ObservableProperty]
    private bool _hasOtherAllergy;

    [ObservableProperty]
    private string _medicalNotes = string.Empty;

    // General Notes
    [ObservableProperty]
    private string _notes = string.Empty;

    // Last confirmed booking
    [ObservableProperty]
    private string _lastBookingNumber = string.Empty;

    [ObservableProperty]
    private string _lastBookingDate = string.Empty;

    [ObservableProperty]
    private string _lastBookingTime = string.Empty;

    public BookingViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadWorkingHoursAsync();
    }

    private async Task LoadWorkingHoursAsync()
    {
        try
        {
            _workingHours = await _apiClient.GetWorkingHoursAsync();
            GenerateTimeSlots();
        }
        catch
        {
            GenerateTimeSlots();
        }
    }

    private void UpdateStepStates()
    {
        IsStep1 = CurrentStep == 1;
        IsStep2 = CurrentStep == 2;
        IsStep3 = CurrentStep == 3;
        IsStepSuccess = CurrentStep == 4;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        GenerateTimeSlots();
    }

    public void GenerateTimeSlots()
    {
        AvailableSlots.Clear();

        if (!TimeSpan.TryParse(_workingHours.StartTime, out var start))
        {
            start = new TimeSpan(17, 0, 0); // 5:00 PM
        }
        if (!TimeSpan.TryParse(_workingHours.EndTime, out var end))
        {
            end = new TimeSpan(22, 30, 0); // 10:30 PM
        }

        var stepMinutes = _workingHours.SlotDurationMinutes > 0 ? _workingHours.SlotDurationMinutes : 30;

        var isToday = SelectedDate.Date == DateTime.Today;
        var nowTime = DateTime.Now.TimeOfDay;

        while (start <= end)
        {
            if (!isToday || start > nowTime.Add(TimeSpan.FromMinutes(15)))
            {
                var dt = DateTime.Today.Add(start);
                AvailableSlots.Add(dt.ToString("hh:mm tt"));
            }
            start = start.Add(TimeSpan.FromMinutes(stepMinutes));
        }

        if (AvailableSlots.Count > 0 && string.IsNullOrEmpty(SelectedTimeSlot))
        {
            SelectedTimeSlot = AvailableSlots[0];
        }
    }

    [RelayCommand]
    public async Task LoadServicesAsync()
    {
        IsLoading = true;
        try
        {
            await LoadWorkingHoursAsync();
            var list = await _apiClient.GetAllServicesAsync();
            Services.Clear();
            foreach (var s in list.Where(x => x.IsActive).OrderBy(x => x.DisplayOrder))
            {
                Services.Add(s);
            }
            if (Services.Count > 0 && SelectedService == null)
            {
                SelectedService = Services[0];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"تعذر تحميل قائمة الخدمات: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NextStep()
    {
        ErrorMessage = string.Empty;
        if (CurrentStep == 1)
        {
            if (SelectedService == null)
            {
                ErrorMessage = "يرجى اختيار نوع الخدمة أو الكشف المطلوب";
                return;
            }
            CurrentStep = 2;
            UpdateStepStates();
            GenerateTimeSlots();
        }
        else if (CurrentStep == 2)
        {
            if (string.IsNullOrEmpty(SelectedTimeSlot))
            {
                ErrorMessage = "يرجى اختيار الوقت المناسب للحجز";
                return;
            }
            CurrentStep = 3;
            UpdateStepStates();
        }
    }

    [RelayCommand]
    private void PrevStep()
    {
        ErrorMessage = string.Empty;
        if (CurrentStep > 1 && CurrentStep <= 3)
        {
            CurrentStep--;
            UpdateStepStates();
        }
    }

    [RelayCommand]
    private async Task ConfirmBookingAsync()
    {
        if (!PatientSession.PatientId.HasValue || SelectedService == null || string.IsNullOrEmpty(SelectedTimeSlot))
        {
            ErrorMessage = "بيانات الحجز غير مكتملة، يرجى التحقق من المدخلات";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            // Build Medical History Summary
            var medList = new List<string>();
            if (HasDiabetes) medList.Add("مرض السكري");
            if (HasHypertension) medList.Add("ضغط دم مرتفع");
            if (HasHeartOrBloodThinners) medList.Add("أمراض قلب/سيولة");
            if (HasPenicillinAllergy) medList.Add("حساسية بنسلين");
            if (HasOtherAllergy) medList.Add("حساسية أدوية أخرى");
            if (!string.IsNullOrWhiteSpace(MedicalNotes)) medList.Add(MedicalNotes.Trim());

            var medSummary = medList.Any() ? $"[السجل الصحي: {string.Join("، ", medList)}]" : "[السجل الصحي: سليم بحمد الله]";
            var fullNotes = string.IsNullOrWhiteSpace(Notes) 
                ? $"{medSummary} - حجز عبر تطبيق المريض ({SelectedService.Name})"
                : $"{medSummary} - ملاحظات المريض: {Notes.Trim()}";

            var appointmentDto = new CreateAppointmentDto
            {
                PatientId = PatientSession.PatientId.Value,
                DoctorName = "د. صديق",
                NewPatientFullName = PatientSession.PatientName ?? "مريض",
                NewPatientPhone = PatientSession.PhoneNumber ?? "",
                AppointmentDate = SelectedDate,
                StartTimeString = SelectedTimeSlot,
                ServiceType = SelectedService.Name,
                TotalFees = SelectedService.DefaultPrice,
                DepositAmount = 0,
                ReasonForVisit = fullNotes,
                Notes = fullNotes
            };

            var result = await _apiClient.CreateAppointmentAsync(appointmentDto);
            if (result != null)
            {
                LastBookingNumber = result.AppointmentNumber;
                LastBookingDate = SelectedDate.ToString("yyyy/MM/dd");
                LastBookingTime = SelectedTimeSlot;

                CurrentStep = 4;
                UpdateStepStates();
            }
            else
            {
                ErrorMessage = _apiClient.LastErrorMessage ?? "تعذر تأكيد الحجز، يرجى المحاولة مرة أخرى.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ أثناء الحجز: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task FinishAndGoHomeAsync()
    {
        CurrentStep = 1;
        UpdateStepStates();
        Notes = string.Empty;
        MedicalNotes = string.Empty;
        await Shell.Current.GoToAsync("//HomePage");
    }
}
