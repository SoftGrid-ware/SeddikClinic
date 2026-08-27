using SeddikClinic.Patient.App.Helpers;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage() : this(ServiceHelper.GetService<LoginViewModel>())
    {
    }

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
