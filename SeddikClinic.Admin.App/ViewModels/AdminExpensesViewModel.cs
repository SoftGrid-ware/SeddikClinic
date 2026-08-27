using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Mobile.Shared.Services;

namespace SeddikClinic.Admin.App.ViewModels;

public partial class AdminExpensesViewModel : ObservableObject
{
    private readonly MobileApiClient _apiClient;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private decimal _totalExpensesAmount;

    public ObservableCollection<ExpenseDto> Expenses { get; } = new();

    public AdminExpensesViewModel(MobileApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    public async Task LoadExpensesAsync()
    {
        IsLoading = true;
        try
        {
            var filter = new ExpenseFilterDto { PageIndex = 1, PageSize = 50 };
            var list = await _apiClient.GetExpensesAsync(filter);
            Expenses.Clear();
            decimal sum = 0;
            foreach (var e in list)
            {
                Expenses.Add(e);
                sum += e.Amount;
            }
            TotalExpensesAmount = sum;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdminExpenses Error]: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
