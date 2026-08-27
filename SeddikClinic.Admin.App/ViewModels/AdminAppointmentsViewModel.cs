using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Enums;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App.ViewModels;

public partial class SelectableServiceItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _quantity = 1;
}

public partial class AdminAppointmentsViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isMultiServiceModalOpen;

    [ObservableProperty]
    private AppointmentDto? _selectedAppointment;

    [ObservableProperty]
    private string _serviceSearchText = string.Empty;

    [ObservableProperty]
    private decimal _totalSelectedServicesAmount;

    [ObservableProperty]
    private int _selectedServicesCount;

    public ObservableCollection<AppointmentDto> Appointments { get; } = new();
    public ObservableCollection<SelectableServiceItem> AllServicesList { get; } = new();
    public ObservableCollection<SelectableServiceItem> FilteredServicesList { get; } = new();

    public AdminAppointmentsViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadAppointmentsAsync();
    }

    [RelayCommand]
    public async Task LoadAppointmentsAsync()
    {
        IsLoading = true;
        try
        {
            var results = await _apiClient.GetAppointmentsAsync(date: SelectedDate);
            Appointments.Clear();
            foreach (var a in results)
            {
                Appointments.Add(a);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdminAppointments Error]: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenAppointmentActionsAsync(AppointmentDto? apt)
    {
        if (apt == null) return;

        string choice = await Shell.Current.DisplayActionSheet(
            $"إجراءات الموعد: {apt.PatientName}",
            "إلغاء",
            null,
            "إضافة وتعديل الخدمات والرسوم 💰 (خدمات متعددة)",
            "تحديث حالة الموعد ⚡",
            "عرض الملاحظات والسجل الطبي 📄",
            "اتصال هاتف بالمريض 📞");

        if (string.IsNullOrEmpty(choice) || choice == "إلغاء") return;

        if (choice.StartsWith("إضافة وتعديل الخدمات"))
        {
            await OpenMultiServiceModalAsync(apt);
        }
        else if (choice.StartsWith("تحديث حالة الموعد"))
        {
            await ChangeStatusAsync(apt);
        }
        else if (choice.StartsWith("عرض الملاحظات"))
        {
            await ShowNotesAsync(apt);
        }
        else if (choice.StartsWith("اتصال هاتف"))
        {
            if (!string.IsNullOrWhiteSpace(apt.PatientPhone) && PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(apt.PatientPhone);
            }
        }
    }

    private async Task ChangeStatusAsync(AppointmentDto apt)
    {
        string newStatusName = await Shell.Current.DisplayActionSheet(
            "اختر الحالة الجديدة للموعد:",
            "إلغاء",
            null,
            "مؤكد (Confirmed) ✅",
            "في الانتظار (In Waiting) ⏳",
            "جاري الكشف (In Progress) 🩺",
            "مكتمل (Completed) 🏁",
            "تم الإلغاء (Cancelled) ❌");

        if (string.IsNullOrEmpty(newStatusName) || newStatusName == "إلغاء") return;

        AppointmentStatus targetStatus = AppointmentStatus.Scheduled;
        string? cancellationReason = null;

        if (newStatusName.StartsWith("مؤكد")) targetStatus = AppointmentStatus.Confirmed;
        else if (newStatusName.StartsWith("في الانتظار")) targetStatus = AppointmentStatus.Waiting;
        else if (newStatusName.StartsWith("جاري الكشف")) targetStatus = AppointmentStatus.InProgress;
        else if (newStatusName.StartsWith("مكتمل")) targetStatus = AppointmentStatus.Completed;
        else if (newStatusName.StartsWith("تم الإلغاء"))
        {
            targetStatus = AppointmentStatus.Cancelled;
            cancellationReason = await Shell.Current.DisplayPromptAsync(
                "سبب الإلغاء",
                "يرجى توضيح سبب إلغاء الحجز (سيظهر للمريض في التطبيق):",
                "تأكيد الإلغاء",
                "تراجع",
                initialValue: "اعتذار من العيادة لظرف طارئ");

            if (cancellationReason == null) return;
        }

        IsLoading = true;
        try
        {
            var success = await _apiClient.UpdateAppointmentStatusAsync(apt.Id, targetStatus, cancellationReason);
            if (success)
            {
                await LoadAppointmentsAsync();
                await Shell.Current.DisplayAlert("نجاح", "تم تحديث حالة الموعد بنجاح", "حسناً");
            }
            else
            {
                await Shell.Current.DisplayAlert("خطأ", "تعذر تحديث حالة الموعد", "حسناً");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task OpenMultiServiceModalAsync(AppointmentDto apt)
    {
        SelectedAppointment = apt;
        IsLoading = true;
        try
        {
            var catalog = await _apiClient.GetAllServicesAsync();
            AllServicesList.Clear();
            FilteredServicesList.Clear();

            var currentServices = (apt.ServiceType ?? "").Split(new[] { "+", ",", "،" }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

            foreach (var s in catalog.Where(x => x.IsActive))
            {
                bool isPreSelected = currentServices.Any(cs => cs.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
                var item = new SelectableServiceItem
                {
                    Id = s.Id,
                    Name = s.Name,
                    CategoryName = s.Category ?? "خدمات عامة",
                    DefaultPrice = s.DefaultPrice,
                    Price = s.DefaultPrice,
                    IsSelected = isPreSelected
                };
                AllServicesList.Add(item);
                FilteredServicesList.Add(item);
            }

            RecalculateTotals();
            IsMultiServiceModalOpen = true;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("خطأ", $"تعذر تحميل الخدمات: {ex.Message}", "حسناً");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void FilterServices()
    {
        FilteredServicesList.Clear();
        var query = ServiceSearchText?.Trim().ToLower() ?? "";
        foreach (var item in AllServicesList)
        {
            if (string.IsNullOrWhiteSpace(query) || item.Name.ToLower().Contains(query) || item.CategoryName.ToLower().Contains(query))
            {
                FilteredServicesList.Add(item);
            }
        }
    }

    [RelayCommand]
    public void ToggleService(SelectableServiceItem item)
    {
        if (item == null) return;
        item.IsSelected = !item.IsSelected;
        RecalculateTotals();
    }

    [RelayCommand]
    public async Task AdjustServicePriceAsync(SelectableServiceItem item)
    {
        if (item == null) return;

        string priceInput = await Shell.Current.DisplayPromptAsync(
            $"سعر خدمة: {item.Name}",
            "أدخل السعر المخصص للخدمة (ج.م):",
            "تطبيق",
            "إلغاء",
            keyboard: Keyboard.Numeric,
            initialValue: item.Price.ToString("0"));

        if (decimal.TryParse(priceInput, out var newPrice))
        {
            item.Price = newPrice;
            item.IsSelected = true;
            RecalculateTotals();
        }
    }

    public void RecalculateTotals()
    {
        var selected = AllServicesList.Where(s => s.IsSelected).ToList();
        SelectedServicesCount = selected.Count;
        TotalSelectedServicesAmount = selected.Sum(s => s.Price);
    }

    [RelayCommand]
    public async Task SaveMultiServicesAsync()
    {
        if (SelectedAppointment == null) return;

        var selected = AllServicesList.Where(s => s.IsSelected).ToList();
        if (selected.Count == 0)
        {
            bool proceedEmpty = await Shell.Current.DisplayAlert("تنبيه", "لم تقم باختيار أي خدمة. هل تريد إلغاء الخدمات المسجلة؟", "نعم", "تراجع");
            if (!proceedEmpty) return;
        }

        string combinedNames = selected.Count > 0 
            ? string.Join(" + ", selected.Select(s => s.Name))
            : "كشف واستشارة طبية";

        decimal totalFees = selected.Count > 0 ? TotalSelectedServicesAmount : 0;

        IsLoading = true;
        try
        {
            var success = await _apiClient.UpdateAppointmentServiceAsync(SelectedAppointment.Id, combinedNames, totalFees);
            if (success)
            {
                IsMultiServiceModalOpen = false;
                await LoadAppointmentsAsync();
                await Shell.Current.DisplayAlert("تم الحفظ بنجاح 🌟", $"تم تحديث خدمات المريض:\n{combinedNames}\n\nالإجمالي: {totalFees:N0} ج.م ومزامنتها في كافة التطبيقات فوراً.", "حسناً");
            }
            else
            {
                await Shell.Current.DisplayAlert("خطأ", "تعذر حفظ التعديلات في السيرفر", "حسناً");
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
    public void CloseMultiServiceModal()
    {
        IsMultiServiceModalOpen = false;
    }

    private async Task ShowNotesAsync(AppointmentDto apt)
    {
        var content = !string.IsNullOrWhiteSpace(apt.Notes) ? apt.Notes : (!string.IsNullOrWhiteSpace(apt.ReasonForVisit) ? apt.ReasonForVisit : "لا توجد ملاحظات مسجلة");
        await Shell.Current.DisplayAlert($"ملاحظات المريض: {apt.PatientName}", content, "إغلاق");
    }
}
