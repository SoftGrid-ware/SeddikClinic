using SeddikClinic.Admin.App.Helpers;
using SeddikClinic.Admin.App.ViewModels;

namespace SeddikClinic.Admin.App.Pages;

public partial class AdminExpensesPage : ContentPage
{
    private readonly AdminExpensesViewModel _vm;

    public AdminExpensesPage() : this(ServiceHelper.GetService<AdminExpensesViewModel>())
    {
    }

    public AdminExpensesPage(AdminExpensesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadExpensesAsync();
    }
}
