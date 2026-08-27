using System.Windows;
using System.Windows.Controls;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class ClinicServicesManagementView : UserControl
{
    private readonly ClinicApiClient _apiClient;
    private List<ClinicServiceDto> _services = new();
    private ClinicServiceDto? _editingService;

    private List<string> _categories = new() { "علاج وتجميل", "كشف وفحص", "وقاية وتجميل", "تركيبات", "جراحة وزراعة", "تقويم", "عام" };

    public ClinicServicesManagementView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        Loaded += async (s, e) => await LoadServicesAsync();
    }

    public async Task LoadServicesAsync()
    {
        try
        {
            _services = await _apiClient.GetClinicServicesAsync();
            ServicesGrid.ItemsSource = _services;

            // دمج التصنيفات الموجودة في الخدمات مع الافتراضية
            foreach (var s in _services)
            {
                if (!string.IsNullOrWhiteSpace(s.Category) && !_categories.Contains(s.Category))
                {
                    _categories.Add(s.Category);
                }
            }

            NewServiceCategoryCombo.ItemsSource = null;
            NewServiceCategoryCombo.ItemsSource = _categories.ToList();
            if (_categories.Any()) NewServiceCategoryCombo.SelectedIndex = 0;

            var consultation = _services.FirstOrDefault(s => s.Name.Contains("كشف"));
            if (consultation != null)
            {
                QuickConsultationInput.Text = consultation.DefaultPrice.ToString("0");
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب الخدمات: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshCategoryLists()
    {
        CategoriesItemsControl.ItemsSource = null;
        CategoriesItemsControl.ItemsSource = _categories.ToList();

        NewServiceCategoryCombo.ItemsSource = null;
        NewServiceCategoryCombo.ItemsSource = _categories.ToList();
        if (_categories.Any() && NewServiceCategoryCombo.SelectedItem == null)
        {
            NewServiceCategoryCombo.SelectedIndex = 0;
        }
    }

    private void ToggleAddCategoryModal_Click(object sender, RoutedEventArgs e)
    {
        NewCategoryNameInput.Text = "";
        RefreshCategoryLists();
        AddServiceCategoryModal.Visibility = Visibility.Visible;
        NewCategoryNameInput.Focus();
    }

    private void CloseAddCategoryModal_Click(object sender, RoutedEventArgs e)
    {
        AddServiceCategoryModal.Visibility = Visibility.Collapsed;
    }

    private void SaveServiceCategory_Click(object sender, RoutedEventArgs e)
    {
        var catName = NewCategoryNameInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(catName))
        {
            ClinicMessageBox.Show("يرجى كتابة اسم تصنيف الخدمات.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_categories.Contains(catName))
        {
            _categories.Add(catName);
        }

        RefreshCategoryLists();
        NewServiceCategoryCombo.SelectedItem = catName;
        NewCategoryNameInput.Text = "";

        ClinicMessageBox.Show($"تمت إضافة تصنيف '{catName}' بنجاح وتعيينه في قائمة الخدمات!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string catName)
        {
            if (_categories.Count <= 1)
            {
                ClinicMessageBox.Show("لا يمكن حذف كافة التصنيفات، يجب أن يبقى تصنيف واحد على الأقل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = ClinicMessageBox.Show($"هل أنت متأكد من حذف تصنيف '{catName}' من قائمة تصنيفات الخدمات؟", "تأكيد حذف التصنيف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                _categories.Remove(catName);
                RefreshCategoryLists();
                ClinicMessageBox.Show($"تم حذف تصنيف '{catName}' بنجاح.", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private async void RefreshServices_Click(object sender, RoutedEventArgs e)
    {
        await LoadServicesAsync();
    }

    private void ToggleAddServicePanel_Click(object sender, RoutedEventArgs e)
    {
        AddServicePanel.Visibility = AddServicePanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void SaveConsultationPrice_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(QuickConsultationInput.Text.Trim(), out var newPrice) || newPrice <= 0)
        {
            ClinicMessageBox.Show("يرجى إدخال سعر كشف صحيح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var success = await _apiClient.UpdateConsultationPriceAsync(newPrice);
            if (success)
            {
                await LoadServicesAsync();
                ClinicMessageBox.Show($"تم تحديث سعر الكشف إلى {newPrice:N0} ج.م بنجاح!", "تم التحديث", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ClinicMessageBox.Show("تعذر تحديث سعر الكشف.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveNewService_Click(object sender, RoutedEventArgs e)
    {
        var name = NewServiceNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ClinicMessageBox.Show("يرجى كتابة اسم الخدمة الطبية.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(NewServicePriceInput.Text.Trim(), out var price) || price < 0)
        {
            ClinicMessageBox.Show("يرجى إدخال سعر صحيح للخدمة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var category = NewServiceCategoryCombo.SelectedItem?.ToString() ?? "عام";

        try
        {
            var created = await _apiClient.CreateClinicServiceAsync(new CreateClinicServiceDto
            {
                Name = name,
                DefaultPrice = price,
                Category = category
            });

            if (created != null)
            {
                NewServiceNameInput.Clear();
                NewServicePriceInput.Text = "500";
                AddServicePanel.Visibility = Visibility.Collapsed;

                await LoadServicesAsync();
                ClinicMessageBox.Show($"تمت إضافة خدمة '{name}' بسعر {price:N0} ج.م بنجاح!", "تمت الإضافة", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"حدث خطأ أثناء الإضافة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ClinicServiceDto service)
        {
            _editingService = service;
            ModalServiceNameInput.Text = service.Name;
            ModalServicePriceInput.Text = service.DefaultPrice.ToString("0");
            EditServiceModal.Visibility = Visibility.Visible;
        }
    }

    private async void SaveModalEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_editingService == null) return;

        var name = ModalServiceNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ClinicMessageBox.Show("اسم الخدمة مطلوب.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(ModalServicePriceInput.Text.Trim(), out var price) || price < 0)
        {
            ClinicMessageBox.Show("يرجى إدخال سعر صحيح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var updated = await _apiClient.UpdateClinicServiceAsync(_editingService.Id, new UpdateClinicServiceDto
            {
                Name = name,
                DefaultPrice = price,
                Category = _editingService.Category,
                IsActive = true,
                DisplayOrder = _editingService.DisplayOrder
            });

            if (updated != null)
            {
                EditServiceModal.Visibility = Visibility.Collapsed;
                await LoadServicesAsync();
                ClinicMessageBox.Show("تم تحديث الخدمة بنجاح!", "تم التعديل", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelModalEdit_Click(object sender, RoutedEventArgs e)
    {
        EditServiceModal.Visibility = Visibility.Collapsed;
    }

    private async void DeleteService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ClinicServiceDto service)
        {
            var result = ClinicMessageBox.Show($"هل أنت متأكد من حذف خدمة '{service.Name}' من قائمة خدمات العيادة؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = await _apiClient.DeleteClinicServiceAsync(service.Id);
                    if (success)
                    {
                        await LoadServicesAsync();
                    }
                    else
                    {
                        ClinicMessageBox.Show("تعذر حذف الخدمة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    ClinicMessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
