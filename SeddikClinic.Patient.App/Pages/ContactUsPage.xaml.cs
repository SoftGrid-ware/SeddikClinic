using SeddikClinic.Patient.App.Helpers;
using SeddikClinic.Patient.App.ViewModels;

namespace SeddikClinic.Patient.App.Pages;

public partial class ContactUsPage : ContentPage
{
    private readonly ContactUsViewModel _vm;

    public ContactUsPage() : this(ServiceHelper.GetService<ContactUsViewModel>())
    {
    }

    public ContactUsPage(ContactUsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
