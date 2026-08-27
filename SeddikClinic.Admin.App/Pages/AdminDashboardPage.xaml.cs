using SeddikClinic.Admin.App.Helpers;
using SeddikClinic.Admin.App.ViewModels;

namespace SeddikClinic.Admin.App.Pages;

public partial class AdminDashboardPage : ContentPage
{
    private readonly AdminDashboardViewModel _vm;

    public AdminDashboardPage() : this(ServiceHelper.GetService<AdminDashboardViewModel>())
    {
    }

    public AdminDashboardPage(AdminDashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDashboardAsync();
    }
}
