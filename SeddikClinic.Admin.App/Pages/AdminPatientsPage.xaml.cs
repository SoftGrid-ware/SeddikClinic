using SeddikClinic.Admin.App.Helpers;
using SeddikClinic.Admin.App.ViewModels;

namespace SeddikClinic.Admin.App.Pages;

public partial class AdminPatientsPage : ContentPage
{
    private readonly AdminPatientsViewModel _vm;

    public AdminPatientsPage() : this(ServiceHelper.GetService<AdminPatientsViewModel>())
    {
    }

    public AdminPatientsPage(AdminPatientsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.SearchPatientsAsync();
    }
}
