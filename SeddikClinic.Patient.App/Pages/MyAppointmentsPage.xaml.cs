using SeddikClinic.Patient.App.Helpers;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App.Pages;

public partial class MyAppointmentsPage : ContentPage
{
    private readonly MyAppointmentsViewModel _vm;

    public MyAppointmentsPage() : this(ServiceHelper.GetService<MyAppointmentsViewModel>())
    {
    }

    public MyAppointmentsPage(MyAppointmentsViewModel vm)
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
