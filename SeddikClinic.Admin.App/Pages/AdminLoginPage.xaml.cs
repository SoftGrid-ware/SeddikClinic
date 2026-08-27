using SeddikClinic.Admin.App.Helpers;
using SeddikClinic.Admin.App.ViewModels;

namespace SeddikClinic.Admin.App.Pages;

public partial class AdminLoginPage : ContentPage
{
    private readonly AdminLoginViewModel _vm;

    public AdminLoginPage() : this(ServiceHelper.GetService<AdminLoginViewModel>())
    {
    }

    public AdminLoginPage(AdminLoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
