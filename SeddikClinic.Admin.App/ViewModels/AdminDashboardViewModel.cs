using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App.ViewModels;

public partial class AdminDashboardViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;
    private readonly IDispatcherTimer _pollTimer;
    private int _prevTodayCount = -1;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private decimal _totalRevenue;

    [ObservableProperty]
    private decimal _totalExpenses;

    [ObservableProperty]
    private decimal _netProfit;

    [ObservableProperty]
    private decimal _totalUncollectedReceivables;

    [ObservableProperty]
    private decimal _totalDownPayments;

    [ObservableProperty]
    private int _todayAppointmentsCount;

    [ObservableProperty]
    private int _waitingCount;

    [ObservableProperty]
    private int _completedCount;

    public AdminDashboardViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;

        _pollTimer = Application.Current?.Dispatcher.CreateTimer() ?? new DispatcherTimerWrapper();
        _pollTimer.Interval = TimeSpan.FromSeconds(6);
        _pollTimer.Tick += async (s, e) => await CheckForNewBookingsAsync();
        _pollTimer.Start();
    }

    private async Task CheckForNewBookingsAsync()
    {
        try
        {
            var summary = await _apiClient.GetTodayAppointmentsSummaryAsync();
            if (summary != null)
            {
                if (_prevTodayCount >= 0 && summary.TotalScheduledToday > _prevTodayCount)
                {
                    TodayAppointmentsCount = summary.TotalScheduledToday;
                    WaitingCount = summary.WaitingCount;
                    CompletedCount = summary.CompletedToday;

                    try
                    {
                        if (Vibration.Default.IsSupported)
                        {
                            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500));
                        }
                    }
                    catch { }

                    await Shell.Current.DisplayAlert("حجز مريض جديد", $"تم تسجيل حجز جديد لمريض في العيادة بنجاح!\nإجمالي حجوزات اليوم: {summary.TotalScheduledToday}", "حسناً");
                    await LoadDashboardAsync();
                }
                _prevTodayCount = summary.TotalScheduledToday;
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadDashboardAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        IsLoading = true;
        try
        {
            var data = await _apiClient.GetFinancialDashboardAsync();
            if (data != null)
            {
                TotalRevenue = data.MonthRevenue;
                TotalExpenses = data.MonthExpenses;
                NetProfit = data.MonthNetProfit;
                TotalUncollectedReceivables = data.TotalUncollectedReceivables;
                TotalDownPayments = data.TotalDownPayments;
            }

            var summary = await _apiClient.GetTodayAppointmentsSummaryAsync();
            if (summary != null)
            {
                TodayAppointmentsCount = summary.TotalScheduledToday;
                WaitingCount = summary.WaitingCount;
                CompletedCount = summary.CompletedToday;
                _prevTodayCount = summary.TotalScheduledToday;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdminDashboard Error]: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}

// Fallback timer wrapper
public class DispatcherTimerWrapper : IDispatcherTimer
{
    private readonly System.Timers.Timer _timer = new();
    public TimeSpan Interval { get => TimeSpan.FromMilliseconds(_timer.Interval); set => _timer.Interval = value.TotalMilliseconds; }
    public bool IsRepeating { get => _timer.AutoReset; set => _timer.AutoReset = value; }
    public bool IsRunning => _timer.Enabled;
    public event EventHandler? Tick;

    public DispatcherTimerWrapper()
    {
        _timer.Elapsed += (s, e) => Tick?.Invoke(this, EventArgs.Empty);
    }
    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
}
