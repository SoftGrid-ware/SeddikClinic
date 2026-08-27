using SeddikClinic.Patient.App.Helpers;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public ProfilePage() : this(ServiceHelper.GetService<ProfileViewModel>())
    {
    }

    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadProfile();
    }
}
