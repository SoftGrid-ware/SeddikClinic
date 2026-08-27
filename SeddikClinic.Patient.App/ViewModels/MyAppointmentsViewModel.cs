using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Enums;
using SeddikClinic.Mobile.Shared.Models;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Patient.App.ViewModels;

public partial class MyAppointmentsViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;
    private List<AppointmentDto> _allAppointments = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private string _activeFilter = "All";

    public ObservableCollection<AppointmentDto> FilteredAppointments { get; } = new();

    public MyAppointmentsViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    public async Task LoadAppointmentsAsync()
    {
        if (!PatientSession.PatientId.HasValue) return;

        IsLoading = true;
        try
        {
            _allAppointments = await _apiClient.GetPatientAppointmentsAsync(PatientSession.PatientId.Value);
            ApplyFilter(SelectedFilter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MyAppointmentsVM Error]: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAppointmentsAsync();
    }

    [RelayCommand]
    public void Filter(string filter)
    {
        SelectedFilter = filter;
        ActiveFilter = filter;
        ApplyFilter(filter);
    }

    [RelayCommand]
    public void SetFilter(string filter)
    {
        SelectedFilter = filter;
        ActiveFilter = filter;
        ApplyFilter(filter);
    }

    private void ApplyFilter(string filter)
    {
        FilteredAppointments.Clear();
        var query = filter switch
        {
            "Upcoming" => _allAppointments.Where(a => a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Waiting),
            "Completed" => _allAppointments.Where(a => a.Status == AppointmentStatus.Completed),
            "Cancelled" => _allAppointments.Where(a => a.Status == AppointmentStatus.Cancelled),
            _ => _allAppointments
        };

        foreach (var item in query.OrderByDescending(a => a.AppointmentDate))
        {
            FilteredAppointments.Add(item);
        }
    }

    [RelayCommand]
    public async Task CancelAppointmentAsync(AppointmentDto? apt)
    {
        if (apt == null) return;

        bool confirm = await Shell.Current.DisplayAlert("إلغاء الموعد", $"هل أنت متأكد من إلغاء موعدك في {apt.DateFormatted}؟", "نعم، إلغاء", "تراجع");
        if (!confirm) return;

        IsLoading = true;
        var success = await _apiClient.CancelAppointmentAsync(apt.Id);
        if (success)
        {
            await Shell.Current.DisplayAlert("تم الإلغاء", "تم إلغاء الموعد بنجاح", "حسناً");
            await LoadAppointmentsAsync();
        }
        else
        {
            await Shell.Current.DisplayAlert("خطأ", "تعذر إلغاء الموعد", "حسناً");
        }
        IsLoading = false;
    }
}
