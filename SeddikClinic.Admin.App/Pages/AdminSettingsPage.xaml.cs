using SeddikClinic.Admin.App.Helpers;
using SeddikClinic.Admin.App.ViewModels;

namespace SeddikClinic.Admin.App.Pages;

public partial class AdminSettingsPage : ContentPage
{
    private readonly AdminSettingsViewModel _vm;

    public AdminSettingsPage() : this(ServiceHelper.GetService<AdminSettingsViewModel>())
    {
    }

    public AdminSettingsPage(AdminSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadSettings();
    }
}
