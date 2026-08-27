using SeddikClinic.Admin.App.Helpers;
using SeddikClinic.Admin.App.ViewModels;

namespace SeddikClinic.Admin.App.Pages;

public partial class AdminAppointmentsPage : ContentPage
{
    private readonly AdminAppointmentsViewModel _vm;

    public AdminAppointmentsPage() : this(ServiceHelper.GetService<AdminAppointmentsViewModel>())
    {
    }

    public AdminAppointmentsPage(AdminAppointmentsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAppointmentsAsync();
    }
}
