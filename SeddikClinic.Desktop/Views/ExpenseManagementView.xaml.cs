using System.Windows;
using System.Windows.Controls;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class ExpenseManagementView : UserControl
{
    private readonly ClinicApiClient _apiClient;
    private List<ExpenseCategoryDto> _categories = new();

    public ExpenseManagementView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        Loaded += async (s, e) => await InitializeViewAsync();
    }

    public async Task InitializeViewAsync()
    {
        await LoadCategoriesAsync();
        await LoadExpensesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            _categories = await _apiClient.GetExpenseCategoriesAsync();
            
            var filterCats = new List<ExpenseCategoryDto> { new() { Id = Guid.Empty, NameAr = "كل التصنيفات" } };
            filterCats.AddRange(_categories);

            CategoryFilterCombo.ItemsSource = filterCats;
            CategoryFilterCombo.SelectedIndex = 0;

            ExpenseCategoryCombo.ItemsSource = _categories;
            if (_categories.Any()) ExpenseCategoryCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب التصنيفات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task LoadExpensesAsync()
    {
        try
        {
            var filter = new ExpenseFilterDto
            {
                SearchTerm = SearchBox?.Text,
                PageIndex = 1,
                PageSize = 200,
                FromDate = StartDatePicker?.SelectedDate,
                ToDate = EndDatePicker?.SelectedDate
            };

            if (CategoryFilterCombo?.SelectedItem is ExpenseCategoryDto selectedCategory && selectedCategory.Id != Guid.Empty)
            {
                filter.CategoryId = selectedCategory.Id;
            }

            var expenses = await _apiClient.GetExpensesAsync(filter);
            ExpensesGrid.ItemsSource = expenses;
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب المصروفات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleAddPanel_Click(object sender, RoutedEventArgs e)
    {
        AddExpensePanel.Visibility = AddExpensePanel.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }

    private async void SaveExpense_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ExpenseTitleInput.Text))
        {
            ClinicMessageBox.Show("يرجى إدخال اسم أو بيان المصروف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(ExpenseAmountInput.Text, out var amount) || amount <= 0)
        {
            ClinicMessageBox.Show("يرجى إدخال مبلغ صحيح أكبر من الصفر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedCat = ExpenseCategoryCombo.SelectedItem as ExpenseCategoryDto;
        if (selectedCat == null)
        {
            ClinicMessageBox.Show("يرجى اختيار تصنيف المصروف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var dto = new CreateExpenseDto
            {
                Title = ExpenseTitleInput.Text.Trim(),
                CategoryId = selectedCat.Id,
                Amount = amount,
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = (ExpensePaymentMethod)(PaymentMethodCombo.SelectedIndex + 1),
                Status = ExpenseStatus.Paid,
                BranchId = Guid.Empty
            };

            await _apiClient.CreateExpenseAsync(dto);
            ClinicMessageBox.Show("تم تسجيل المصروف بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);

            ExpenseTitleInput.Text = "";
            ExpenseAmountInput.Text = "";
            AddExpensePanel.Visibility = Visibility.Collapsed;

            await LoadExpensesAsync();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"فشل حفظ المصروف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CancelExpense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ExpenseDto expense)
        {
            var result = ClinicMessageBox.Show($"هل أنت متأكد من إلغاء المصروف: {expense.Title} بقيمة {expense.Amount:N2} ج.م؟", 
                "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var success = await _apiClient.CancelExpenseAsync(expense.Id, "تم الإلغاء من واجهة الويندوز");
                if (success)
                {
                    await LoadExpensesAsync();
                }
            }
        }
    }

    private async void DeleteExpense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ExpenseDto expense)
        {
            var confirm = ClinicMessageBox.Show($"هل أنت متأكد تماماً من حذف المصروف '{expense.Title}' بقيمة {expense.Amount:N2} ج.م نهائياً من السجلات؟", "تأكيد حذف المصروف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                var success = await _apiClient.DeleteExpenseAsync(expense.Id);
                if (success)
                {
                    ClinicMessageBox.Show("تم حذف المصروف بنجاح!", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadExpensesAsync();
                }
                else
                {
                    ClinicMessageBox.Show("تعذر حذف المصروف.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void ApplyDateFilter_Click(object sender, RoutedEventArgs e)
    {
        await LoadExpensesAsync();
    }

    private async void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = null;
        EndDatePicker.SelectedDate = null;
        await LoadExpensesAsync();
    }

    private async void FilterToday_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = DateTime.Today;
        EndDatePicker.SelectedDate = DateTime.Today;
        await LoadExpensesAsync();
    }

    private async void FilterMonth_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        EndDatePicker.SelectedDate = DateTime.Today;
        await LoadExpensesAsync();
    }

    private void ToggleAddCategoryModal_Click(object sender, RoutedEventArgs e)
    {
        NewCategoryNameInput.Text = "";
        NewCategoryDirectCostCheck.IsChecked = false;
        AddCategoryModal.Visibility = Visibility.Visible;
        NewCategoryNameInput.Focus();
    }

    private void CloseAddCategoryModal_Click(object sender, RoutedEventArgs e)
    {
        AddCategoryModal.Visibility = Visibility.Collapsed;
    }

    private async void SaveCategory_Click(object sender, RoutedEventArgs e)
    {
        var catName = NewCategoryNameInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(catName))
        {
            ClinicMessageBox.Show("يرجى إدخال اسم التصنيف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isDirectCost = NewCategoryDirectCostCheck.IsChecked == true;

        try
        {
            var newCat = await _apiClient.CreateExpenseCategoryAsync(catName, isDirectCost);
            if (newCat != null)
            {
                ClinicMessageBox.Show($"تمت إضافة التصنيف '{newCat.NameAr}' بنجاح!", "نجاح الإضافة", MessageBoxButton.OK, MessageBoxImage.Information);
                AddCategoryModal.Visibility = Visibility.Collapsed;
                await LoadCategoriesAsync();

                // إذا كان فورك إضافة المصروف مفتوحاً، قم باختيار التصنيف الجديد فوراً
                var match = _categories.FirstOrDefault(c => c.Id == newCat.Id);
                if (match != null)
                {
                    ExpenseCategoryCombo.SelectedItem = match;
                }
            }
            else
            {
                ClinicMessageBox.Show("فشل إضافة التصنيف.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء حفظ التصنيف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) await LoadExpensesAsync();
    }

    private async void CategoryFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) await LoadExpensesAsync();
    }

    private async void RefreshExpenses_Click(object sender, RoutedEventArgs e)
    {
        await LoadExpensesAsync();
    }
}
