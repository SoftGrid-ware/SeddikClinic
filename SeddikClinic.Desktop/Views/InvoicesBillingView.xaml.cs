using System.Windows;
using System.Windows.Controls;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class InvoicesBillingView : UserControl
{
    private readonly ClinicApiClient _apiClient;
    private List<AppointmentDto> _allAppointments = new();
    private List<InvoiceItemViewModel> _displayedInvoices = new();
    private InvoiceItemViewModel? _selectedInvoiceForPayment;

    public InvoicesBillingView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        Loaded += async (s, e) =>
        {
            StartDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            EndDatePicker.SelectedDate = DateTime.Today.AddDays(30);
            await LoadInvoicesAsync();
        };
    }

    public async Task LoadInvoicesAsync()
    {
        try
        {
            var start = StartDatePicker.SelectedDate;
            var end = EndDatePicker.SelectedDate;

            _allAppointments = await _apiClient.GetAppointmentsAsync(startDate: start, endDate: end);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء جلب الفواتير والمطالبات: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyFilters()
    {
        var statusIndex = PaymentStatusFilterCombo?.SelectedIndex ?? 0;

        var baseQuery = _allAppointments
            .Where(a => a.Status != Core.Enums.AppointmentStatus.Cancelled);

        // عرض حالات التقسيط الحقيقية فقط:
        // = حجوزات دفع فيها عربون أقل من الإجمالي (أي فيها متبقي للتحصيل)
        // إلا لو اختار "عرض كافة الحجوزات"
        if (statusIndex != 3)
        {
            baseQuery = baseQuery.Where(a =>
                a.DepositAmount > 0 && a.DepositAmount < a.TotalFees);
        }

        var list = baseQuery.Select(a => new InvoiceItemViewModel
        {
            AppointmentId = a.Id,
            AppointmentNumber = a.AppointmentNumber,
            PatientName = a.PatientName,
            PatientPhone = a.PatientPhone,
            AppointmentDate = a.AppointmentDate,
            ServiceType = a.ServiceType,
            TotalFees = a.TotalFees,
            DepositAmount = a.DepositAmount,
            IsDepositPaid = a.IsDepositPaid
        }).ToList();

        // Search filter
        var search = SearchBox.Text?.Trim().ToLower() ?? "";
        if (!string.IsNullOrEmpty(search))
        {
            list = list.Where(i =>
                i.PatientName.ToLower().Contains(search) ||
                i.PatientPhone.Contains(search) ||
                i.AppointmentNumber.ToLower().Contains(search) ||
                i.ServiceType.ToLower().Contains(search)
            ).ToList();
        }

        // Status filter
        if (statusIndex == 1) // متبقي أقساط وذمم جارية
        {
            list = list.Where(i => !i.IsFullyPaid).ToList();
        }
        else if (statusIndex == 2) // أقساط مسددة بالكامل
        {
            list = list.Where(i => i.IsFullyPaid).ToList();
        }

        _displayedInvoices = list.OrderByDescending(i => i.AppointmentDate).ToList();
        InvoicesGrid.ItemsSource = _displayedInvoices;

        // Update KPIs
        var totalCollected = list.Sum(i => i.DepositAmount);
        var totalUncollected = list.Sum(i => i.RemainingAmount);
        var downPayments = list.Where(i => !i.IsFullyPaid && i.DepositAmount > 0).Sum(i => i.DepositAmount);

        KpiCollectedText.Text = $"{totalCollected:N2} ج.م";
        KpiUncollectedText.Text = $"{totalUncollected:N2} ج.م";
        KpiDownPaymentsText.Text = $"{downPayments:N2} ج.م";
        KpiInvoicesCountText.Text = $"{list.Count} حالة تقسيط";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private void FilterStatus_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private async void ApplyDateFilter_Click(object sender, RoutedEventArgs e)
    {
        await LoadInvoicesAsync();
    }

    private async void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = null;
        EndDatePicker.SelectedDate = null;
        await LoadInvoicesAsync();
    }

    private async void FilterToday_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = DateTime.Today;
        EndDatePicker.SelectedDate = DateTime.Today;
        await LoadInvoicesAsync();
    }

    private async void FilterMonth_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Today;
        StartDatePicker.SelectedDate = new DateTime(now.Year, now.Month, 1);
        EndDatePicker.SelectedDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
        await LoadInvoicesAsync();
    }

    private async void RefreshInvoices_Click(object sender, RoutedEventArgs e)
    {
        await LoadInvoicesAsync();
    }

    // =========================================================
    // 💵 تحصيل وسداد دفعة / قسط مالي
    // =========================================================

    private void CollectPayment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is InvoiceItemViewModel inv)
        {
            _selectedInvoiceForPayment = inv;
            ModalPatientNameText.Text = $"المريض: {inv.PatientName} | رقم المطالبة: {inv.AppointmentNumber}";
            ModalTotalFeesText.Text = $"{inv.TotalFees:N2} ج.م";
            ModalPaidAmountText.Text = $"{inv.DepositAmount:N2} ج.م";
            ModalRemainingText.Text = $"{inv.RemainingAmount:N2} ج.م";

            ModalPaymentAmountInput.Text = inv.RemainingAmount > 0 ? inv.RemainingAmount.ToString("0") : "100";
            ModalNotesInput.Text = $"تحصيل قسط لخدمة {inv.ServiceType}";

            CollectPaymentModal.Visibility = Visibility.Visible;
            ModalPaymentAmountInput.Focus();
            ModalPaymentAmountInput.SelectAll();
        }
    }

    private void CloseCollectPaymentModal_Click(object sender, RoutedEventArgs e)
    {
        CollectPaymentModal.Visibility = Visibility.Collapsed;
        _selectedInvoiceForPayment = null;
    }

    private async void ConfirmCollectPayment_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInvoiceForPayment == null) return;

        if (!decimal.TryParse(ModalPaymentAmountInput.Text.Trim(), out var amount) || amount <= 0)
        {
            ClinicMessageBox.Show("يرجى إدخال مبلغ صحيح أكبر من الصفر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var method = (ModalPaymentMethodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "نقداً";
        var notes = ModalNotesInput.Text.Trim();

        try
        {
            var success = await _apiClient.PayInstallmentAsync(_selectedInvoiceForPayment.AppointmentId, amount, method, notes);
            if (success)
            {
                ClinicMessageBox.Show($"تم تحصيل وتسجيل دفعة بقيمة {amount:N2} ج.م للمريض '{_selectedInvoiceForPayment.PatientName}' بنجاح!", "نجاح التحصيل", MessageBoxButton.OK, MessageBoxImage.Information);
                CollectPaymentModal.Visibility = Visibility.Collapsed;
                _selectedInvoiceForPayment = null;
                await LoadInvoicesAsync();
            }
            else
            {
                ClinicMessageBox.Show("تعذر تسجيل التحصيل.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء تسجيل التحصيل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is InvoiceItemViewModel inv)
        {
            var receipt = $"====================================\n" +
                          $"        عيادة الصديق التخصصية        \n" +
                          $"          إيصال سداد ومطالبة         \n" +
                          $"====================================\n" +
                          $"رقم المطالبة: {inv.AppointmentNumber}\n" +
                          $"اسم المريض: {inv.PatientName}\n" +
                          $"التاريخ: {inv.DateFormatted}\n" +
                          $"الخدمة الطبية: {inv.ServiceType}\n" +
                          $"إجمالي القيمة: {inv.TotalFees:N2} ج.م\n" +
                          $"المدفوع / المحصل: {inv.DepositAmount:N2} ج.م\n" +
                          $"المتبقي للتقسيط: {inv.RemainingAmount:N2} ج.م\n" +
                          $"حالة الفاتورة: {inv.PaymentStatusText}\n" +
                          $"====================================\n" +
                          $"شكراً لثقتكم بعيادة الصديق التخصصية";

            ClinicMessageBox.Show(receipt, "معاينة إيصال السداد", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

public class InvoiceItemViewModel
{
    public Guid AppointmentId { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string DateFormatted => AppointmentDate.ToString("yyyy/MM/dd");
    public string ServiceType { get; set; } = string.Empty;
    public decimal TotalFees { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsDepositPaid { get; set; }
    public decimal RemainingAmount => Math.Max(0, TotalFees - DepositAmount);
    // مسدد بالكامل = فقط لو المتبقي صفر (تم دفع الإجمالي كاملاً)
    public bool IsFullyPaid => RemainingAmount == 0;
    public bool IsZeroPaid => DepositAmount == 0;
    public string PaymentStatusText =>
        IsFullyPaid
            ? "مسددة بالكامل ✅"
            : DepositAmount > 0
                ? $"عربون {DepositAmount:N0} ج.م | متبقي {RemainingAmount:N0} ج.م ⏳"
                : "غير مسددة ❌";
}
