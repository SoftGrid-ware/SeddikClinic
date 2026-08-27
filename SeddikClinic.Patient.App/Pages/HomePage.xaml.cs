using SeddikClinic.Patient.App.Helpers;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage() : this(ServiceHelper.GetService<HomeViewModel>())
    {
    }

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDataAsync();
    }
}
