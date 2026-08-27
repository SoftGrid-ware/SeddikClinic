using SeddikClinic.Patient.App.Helpers;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App.Pages;

public partial class BookingPage : ContentPage
{
    private readonly BookingViewModel _vm;

    public BookingPage() : this(ServiceHelper.GetService<BookingViewModel>())
    {
    }

    public BookingPage(BookingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadServicesAsync();
    }
}
