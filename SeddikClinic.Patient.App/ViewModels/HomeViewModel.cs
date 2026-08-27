using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Mobile.Shared.Models;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Patient.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private string _patientName = string.Empty;

    [ObservableProperty]
    private string _welcomeDate = DateTime.Now.ToString("dddd، d MMMM yyyy", new System.Globalization.CultureInfo("ar-EG"));

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasUpcomingAppointments;

    public ObservableCollection<AppointmentDto> UpcomingAppointments { get; } = new();

    public HomeViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        PatientName = PatientSession.PatientName ?? "عزيزنا المريض";
        if (!PatientSession.PatientId.HasValue) return;

        IsLoading = true;
        try
        {
            var appointments = await _apiClient.GetPatientAppointmentsAsync(PatientSession.PatientId.Value);
            var upcoming = appointments
                .Where(a => a.Status == Core.Enums.AppointmentStatus.Scheduled ||
                            a.Status == Core.Enums.AppointmentStatus.Confirmed ||
                            a.Status == Core.Enums.AppointmentStatus.Waiting ||
                            a.Status == Core.Enums.AppointmentStatus.InProgress)
                .OrderBy(a => a.AppointmentDate)
                .Take(3)
                .ToList();

            UpcomingAppointments.Clear();
            foreach (var apt in upcoming)
            {
                UpcomingAppointments.Add(apt);
            }

            HasUpcomingAppointments = UpcomingAppointments.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeVM Error]: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task BookNowAsync()
    {
        await Shell.Current.GoToAsync("//BookingPage");
    }

    [RelayCommand]
    private async Task ViewAllAppointmentsAsync()
    {
        await Shell.Current.GoToAsync("//MyAppointmentsPage");
    }

    [RelayCommand]
    private async Task GoToBookingAsync()
    {
        await Shell.Current.GoToAsync("//BookingPage");
    }

    [RelayCommand]
    private async Task GoToMyAppointmentsAsync()
    {
        await Shell.Current.GoToAsync("//MyAppointmentsPage");
    }
}
