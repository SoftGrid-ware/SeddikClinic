using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.Enums;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public class ChartBarViewModel
{
    public int DayNumber { get; set; }
    public string FormattedDate { get; set; } = string.Empty;
    public double RevenueHeight { get; set; }
    public double ExpenseHeight { get; set; }
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
}

public partial class FinancialDashboardView : UserControl
{
    private readonly ClinicApiClient _apiClient;
    private string _activePeriod = "month";
    private DateTime? _customStart = null;
    private DateTime? _customEnd = null;

    private List<AppointmentDto> _currentAppointments = new();
    private List<ExpenseDto> _currentExpenses = new();
    private FinancialDashboardDto? _dashboardData;

    private string _currentModalType = "";
    private List<AppointmentDto> _activeModalAppointments = new();
    private List<ExpenseDto> _activeModalExpenses = new();

    public FinancialDashboardView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        // تعيين التاريخ المبدئي لـ من وإلى (أول الشهر الحالي إلى اليوم)
        StartDatePicker.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        EndDatePicker.SelectedDate = DateTime.Today;

        Loaded += async (s, e) => await LoadDashboardDataAsync();
    }

    // =========================================================
    // 📅 فلترة الفترات السريعة (اليوم، الأسبوع، الشهر، السنة)
    // =========================================================

    private async void FilterPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _activePeriod = tag;
            _customStart = null;
            _customEnd = null;

            UpdatePresetButtonsVisualState(btn);
            await LoadDashboardDataAsync();
        }
    }

    private void UpdatePresetButtonsVisualState(Button? activeBtn)
    {
        var primaryBg = new SolidColorBrush(Color.FromRgb(2, 132, 199)); // #0284C7
        var outlineBg = Brushes.Transparent;
        var whiteText = Brushes.White;
        var darkText = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // #0F172A

        var buttons = new[] { BtnFilterToday, BtnFilterWeek, BtnFilterMonth, BtnFilterYear };
        foreach (var b in buttons)
        {
            if (b == null) continue;
            if (b == activeBtn)
            {
                b.Background = primaryBg;
                b.Foreground = whiteText;
                b.FontWeight = FontWeights.Bold;
            }
            else
            {
                b.Background = outlineBg;
                b.Foreground = darkText;
                b.FontWeight = FontWeights.Normal;
            }
        }
    }

    // =========================================================
    // 🔍 فلترة مخصصة محددة (من تاريخ وإلى تاريخ)
    // =========================================================

    private async void ApplyCustomRange_Click(object sender, RoutedEventArgs e)
    {
        if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
        {
            ClinicMessageBox.Show("يرجى تحديد تاريخ البداية (من) وتاريخ النهاية (إلى).", "تنبيه الفلترة", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (StartDatePicker.SelectedDate.Value > EndDatePicker.SelectedDate.Value)
        {
            ClinicMessageBox.Show("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _customStart = StartDatePicker.SelectedDate.Value;
        _customEnd = EndDatePicker.SelectedDate.Value;
        _activePeriod = "custom";

        // إلغاء تمييز أزرار الفترات السريعة لأننا نستخدم فترة مخصصة
        UpdatePresetButtonsVisualState(null);

        await LoadDashboardDataAsync();
    }

    public async Task LoadDashboardDataAsync()
    {
        try
        {
            StatusBanner.Visibility = Visibility.Visible;
            StatusText.Text = "جاري جلب المؤشرات والتحليلات المالية من الخادم...";

            var filter = new FinancialFilterDto
            {
                PeriodType = _activePeriod,
                StartDate = _customStart,
                EndDate = _customEnd
            };

            var data = await _apiClient.GetFinancialDashboardAsync(filter, msg =>
            {
                Dispatcher.Invoke(() => StatusText.Text = msg);
            });

            _dashboardData = data;
            var (rangeStart, rangeEnd) = GetActiveDateRange();

            try
            {
                var appointments = await _apiClient.GetAppointmentsAsync(startDate: rangeStart, endDate: rangeEnd);
                _currentAppointments = appointments ?? new();
            }
            catch
            {
                _currentAppointments = new();
            }

            try
            {
                var expenses = await _apiClient.GetExpensesAsync(new ExpenseFilterDto { FromDate = rangeStart, ToDate = rangeEnd, PageSize = 500 });
                _currentExpenses = expenses ?? new();
            }
            catch
            {
                _currentExpenses = new();
            }

            if (data != null)
            {
                // Update Hero Metric Cards
                MonthRevenueText.Text = $"{data.MonthRevenue:N2} ج.م";
                MonthExpensesText.Text = $"{data.MonthExpenses:N2} ج.م";
                NetCashText.Text = $"{data.NetCashFlow:N2} ج.م";
                OperatingProfitText.Text = $"{data.EstimatedOperatingProfit:N2} ج.م";

                // Update Secondary Cards
                TodayRevenueText.Text = $"{data.TodayRevenue:N2} ج.م";
                TodayExpensesText.Text = $"{data.TodayExpenses:N2} ج.م";
                UncollectedReceivablesText.Text = $"{data.TotalUncollectedReceivables:N2} ج.م";
                DownPaymentsText.Text = $"{data.TotalDownPayments:N2} ج.م";

                // Update Clinical & Operations KPI Cards
                var totalVisits = _currentAppointments.Count;
                var completedVisits = _currentAppointments.Count(a => a.Status == AppointmentStatus.Completed);
                var scheduledVisits = _currentAppointments.Count(a => a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.InProgress);
                var cancelledVisits = _currentAppointments.Count(a => a.Status == AppointmentStatus.Cancelled);

                TotalVisitsText.Text = $"{totalVisits} زيارة";
                CompletedVisitsText.Text = $"{completedVisits} كشف";
                ScheduledVisitsText.Text = $"{scheduledVisits} موعد";
                CancelledVisitsText.Text = $"{cancelledVisits} موعد";

                // Breakdowns & Lists
                TopServicesList.ItemsSource = data.TopRevenueServices;
                CategoryBreakdownList.ItemsSource = data.CategoryExpenseBreakdown;

                // Daily Trend Bar Chart
                PopulateTrendChart(data.DailyTrendChart);
            }

            StatusBanner.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"خطأ: {ex.Message}";
            StatusBanner.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
            StatusBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
        }
    }

    private void PopulateTrendChart(List<DailyFinancialPointDto> points)
    {
        if (points == null || !points.Any())
        {
            DailyChartItemsControl.ItemsSource = null;
            return;
        }

        var maxVal = Math.Max(
            points.Max(p => p.Revenue),
            points.Max(p => p.Expenses)
        );

        if (maxVal <= 0) maxVal = 1;

        var chartBars = points.Select(p => new ChartBarViewModel
        {
            DayNumber = p.Date.Day,
            FormattedDate = p.FormattedDate,
            Revenue = p.Revenue,
            Expenses = p.Expenses,
            RevenueHeight = Math.Min(120, Math.Max(4, (double)(p.Revenue / maxVal) * 120)),
            ExpenseHeight = Math.Min(120, Math.Max(4, (double)(p.Expenses / maxVal) * 120))
        }).ToList();

        DailyChartItemsControl.ItemsSource = chartBars;
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadDashboardDataAsync();
    }

    private async void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _apiClient.ExportExpensesExcelAsync();
            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel CSV (*.csv)|*.csv",
                FileName = $"Expenses_Report_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(saveDialog.FileName, bytes);
                ClinicMessageBox.Show("تم تصدير التقرير المالي بنجاح!", "نجاح التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"فشل التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintReport_Click(object sender, RoutedEventArgs e)
    {
        ClinicMessageBox.Show("جاري تجهيز تقرير الأرباح والمصروفات للطباعة أو الحفظ كـ PDF.", "طباعة التقرير", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // =========================================================
    // 🔍 نافذة تفاصيل البطاقات المالية والتشغيلية (Drilldown)
    // =========================================================

    private (DateTime? start, DateTime? end) GetActiveDateRange()
    {
        if (_activePeriod == "today")
            return (DateTime.Today, DateTime.Today);
        if (_activePeriod == "week")
            return (DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek), DateTime.Today);
        if (_activePeriod == "month")
            return (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
        if (_activePeriod == "year")
            return (new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today);
        if (_activePeriod == "custom" && _customStart.HasValue && _customEnd.HasValue)
            return (_customStart.Value, _customEnd.Value);

        return (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
    }

    private void HeroCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string tag) return;
        OpenDetailsModal(tag);
    }

    private void OpenDetailsModal(string tag)
    {
        _currentModalType = tag;
        if (DetailsSearchBox != null) DetailsSearchBox.Text = "";

        AppointmentsDetailsContainer.Visibility = Visibility.Collapsed;
        ExpensesDetailsContainer.Visibility = Visibility.Collapsed;
        ExplainerDetailsContainer.Visibility = Visibility.Collapsed;

        var (start, end) = GetActiveDateRange();
        var dateRangeStr = start.HasValue && end.HasValue
            ? $"الفترة من: {start.Value:yyyy/MM/dd} إلى: {end.Value:yyyy/MM/dd}"
            : "كافة الفترات";

        switch (tag)
        {
            case "Revenue":
                DetailsIconText.Text = "💵";
                DetailsTitleText.Text = "تفاصيل الإيرادات والمقبوضات المحصلة";
                DetailsSubtitleText.Text = $"{dateRangeStr} • مجموع المدفوعات المسددة فقط";
                _activeModalAppointments = _currentAppointments.Where(a => a.TotalFees > 0 || a.Status == AppointmentStatus.Completed).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _dashboardData?.MonthRevenue ?? _activeModalAppointments.Sum(a => a.TotalFees));
                break;

            case "Expenses":
                DetailsIconText.Text = "💸";
                DetailsTitleText.Text = "تفاصيل المصروفات المدفوعة";
                DetailsSubtitleText.Text = $"{dateRangeStr} • المصروفات المسددة من رصيد العيادة";
                _activeModalExpenses = _currentExpenses.ToList();
                ExpensesDetailsContainer.Visibility = Visibility.Visible;
                ApplyExpensesModalData(_activeModalExpenses, _dashboardData?.MonthExpenses ?? _activeModalExpenses.Sum(x => x.Amount));
                break;

            case "NetCash":
                DetailsIconText.Text = "📈";
                DetailsTitleText.Text = "تفاصيل واحتساب صافي التدفق النقدي (الكاش)";
                DetailsSubtitleText.Text = $"{dateRangeStr} • المحصل الفعلي مطروحاً منه المصروفات المدفوعة";
                ExplainerDetailsContainer.Visibility = Visibility.Visible;
                ExplainerHeader.Text = "معادلة صافي التدفق النقدي الفعلي:";
                var rev = _dashboardData?.MonthRevenue ?? 0m;
                var exp = _dashboardData?.MonthExpenses ?? 0m;
                var net = _dashboardData?.NetCashFlow ?? (rev - exp);
                ExplainerText.Text = $"• إجمالي الإيرادات المحصلة نقداً: {rev:N2} ج.م\n" +
                                     $"• إجمالي المصروفات التشغيلية المدفوعة: {exp:N2} ج.م\n" +
                                     $"───────────────────────────────────\n" +
                                     $"= صافي السيولة النقدية المتبقية: {net:N2} ج.م\n\n" +
                                     $"💡 ملاحظة: هذا الرقم يمثل الفارق الحقيقي بين ما دخل خزانة العيادة وما خرج منها خلال الفترة المحددة.";
                DetailsCountText.Text = "معادلة نقدية";
                DetailsTotalAmountText.Text = $"{net:N2} ج.م";
                break;

            case "OperatingProfit":
                DetailsIconText.Text = "🛡️";
                DetailsTitleText.Text = "تفاصيل واحتساب الربح التشغيلي التقديري";
                DetailsSubtitleText.Text = $"{dateRangeStr} • قيمة الخدمات الطبية مطروحاً منها تكاليف المعمل والمواد";
                ExplainerDetailsContainer.Visibility = Visibility.Visible;
                ExplainerHeader.Text = "معادلة الربح التشغيلي التقديري:";
                var opRev = _dashboardData?.MonthRevenue ?? 0m;
                var opProfit = _dashboardData?.EstimatedOperatingProfit ?? 0m;
                ExplainerText.Text = $"• قيمة إجمالي الخدمات الطبية المنفذة: {opRev:N2} ج.م\n" +
                                     $"• التكاليف التقديرية (معامل، مستلزمات طبية، خامات): {(opRev - opProfit):N2} ج.م\n" +
                                     $"───────────────────────────────────\n" +
                                     $"= الربح التشغيلي التقديري: {opProfit:N2} ج.م\n\n" +
                                     $"💡 ملاحظة: الربح التشغيلي يقيس العائد الطبي المباشر بعد خصم المصاريف المباشرة للخدمات.";
                DetailsCountText.Text = "مؤشر تشغيلي";
                DetailsTotalAmountText.Text = $"{opProfit:N2} ج.م";
                break;

            case "TodayRevenue":
                DetailsIconText.Text = "☀️";
                DetailsTitleText.Text = "تفاصيل إيرادات ومقبوضات اليوم";
                DetailsSubtitleText.Text = $"تاريخ اليوم: {DateTime.Today:yyyy/MM/dd}";
                _activeModalAppointments = _currentAppointments.Where(a => a.AppointmentDate.Date == DateTime.Today.Date).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _dashboardData?.TodayRevenue ?? _activeModalAppointments.Sum(a => a.TotalFees));
                break;

            case "TodayExpenses":
                DetailsIconText.Text = "💸";
                DetailsTitleText.Text = "تفاصيل مصروفات اليوم";
                DetailsSubtitleText.Text = $"تاريخ اليوم: {DateTime.Today:yyyy/MM/dd}";
                _activeModalExpenses = _currentExpenses.Where(x => x.PaymentDate.Date == DateTime.Today.Date).ToList();
                ExpensesDetailsContainer.Visibility = Visibility.Visible;
                ApplyExpensesModalData(_activeModalExpenses, _dashboardData?.TodayExpenses ?? _activeModalExpenses.Sum(x => x.Amount));
                break;

            case "Uncollected":
                DetailsIconText.Text = "⏳";
                DetailsTitleText.Text = "المبالغ المستحقة غير المحصلة (المتبقي على المرضى)";
                DetailsSubtitleText.Text = $"{dateRangeStr} • مبالغ متبقية على المرضى بعد الحجز أو الكشف";
                _activeModalAppointments = _currentAppointments.Where(a => a.Status != AppointmentStatus.Cancelled && (a.TotalFees - a.DepositAmount) > 0).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _dashboardData?.TotalUncollectedReceivables ?? _activeModalAppointments.Sum(a => a.TotalFees - a.DepositAmount));
                break;

            case "DownPayments":
                DetailsIconText.Text = "💰";
                DetailsTitleText.Text = "تفاصيل العربون والدفعات الجزئية المقدمة";
                DetailsSubtitleText.Text = $"{dateRangeStr} • دفعات الحجز المقدمة قبل موعد الكشف";
                _activeModalAppointments = _currentAppointments.Where(a => a.DepositAmount > 0).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _dashboardData?.TotalDownPayments ?? _activeModalAppointments.Sum(a => a.DepositAmount));
                break;

            case "TotalVisits":
                DetailsIconText.Text = "👥";
                DetailsTitleText.Text = "سجل كافة الحجوزات والزيارات المسجلة";
                DetailsSubtitleText.Text = $"{dateRangeStr} • كافة المواعيد بجميع الحالات";
                _activeModalAppointments = _currentAppointments.ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _activeModalAppointments.Sum(a => a.TotalFees));
                break;

            case "CompletedVisits":
                DetailsIconText.Text = "✅";
                DetailsTitleText.Text = "سجل الكشوفات المنتهية والمكتملة مع الطبيب";
                DetailsSubtitleText.Text = $"{dateRangeStr} • المرضى الذين أتموا الكشف الطبي";
                _activeModalAppointments = _currentAppointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _activeModalAppointments.Sum(a => a.TotalFees));
                break;

            case "ScheduledVisits":
                DetailsIconText.Text = "⏳";
                DetailsTitleText.Text = "جدول المواعيد المجدولة وقيد الانتظار";
                DetailsSubtitleText.Text = $"{dateRangeStr} • مواعيد مؤكدة في صالة الانتظار أو قادمة";
                _activeModalAppointments = _currentAppointments.Where(a => a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.InProgress).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _activeModalAppointments.Sum(a => a.TotalFees));
                break;

            case "CancelledVisits":
                DetailsIconText.Text = "❌";
                DetailsTitleText.Text = "سجل المواعيد والزيارات الملغاة";
                DetailsSubtitleText.Text = $"{dateRangeStr} • مواعيد تم إلغاؤها من المريض أو العيادة";
                _activeModalAppointments = _currentAppointments.Where(a => a.Status == AppointmentStatus.Cancelled).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _activeModalAppointments.Sum(a => a.TotalFees));
                break;
        }

        FinancialDetailsModal.Visibility = Visibility.Visible;
    }

    private void ApplyAppointmentsModalData(List<AppointmentDto> items, decimal totalSum)
    {
        ModalAppointmentsGrid.ItemsSource = items;
        DetailsCountText.Text = $"{items.Count} كشف / موعد";
        DetailsTotalAmountText.Text = $"{totalSum:N2} ج.م";
    }

    private void ApplyExpensesModalData(List<ExpenseDto> items, decimal totalSum)
    {
        ModalExpensesGrid.ItemsSource = items;
        DetailsCountText.Text = $"{items.Count} إيصال مصروف";
        DetailsTotalAmountText.Text = $"{totalSum:N2} ج.م";
    }

    private void DetailsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = DetailsSearchBox.Text?.Trim().ToLower() ?? "";

        if (AppointmentsDetailsContainer.Visibility == Visibility.Visible)
        {
            var filtered = string.IsNullOrWhiteSpace(text)
                ? _activeModalAppointments
                : _activeModalAppointments.Where(a =>
                    a.PatientName.ToLower().Contains(text) ||
                    a.PatientPhone.Contains(text) ||
                    a.ServiceType.ToLower().Contains(text)).ToList();

            ModalAppointmentsGrid.ItemsSource = filtered;
            DetailsCountText.Text = $"{filtered.Count} كشف / موعد";
        }
        else if (ExpensesDetailsContainer.Visibility == Visibility.Visible)
        {
            var filtered = string.IsNullOrWhiteSpace(text)
                ? _activeModalExpenses
                : _activeModalExpenses.Where(x =>
                    x.Title.ToLower().Contains(text) ||
                    (x.BeneficiaryName?.ToLower().Contains(text) ?? false) ||
                    (x.ReceiptNumber?.ToLower().Contains(text) ?? false) ||
                    (x.CategoryNameAr?.ToLower().Contains(text) ?? false)).ToList();

            ModalExpensesGrid.ItemsSource = filtered;
            DetailsCountText.Text = $"{filtered.Count} إيصال مصروف";
        }
    }

    private void CloseFinancialDetailsModal_Click(object sender, RoutedEventArgs e)
    {
        FinancialDetailsModal.Visibility = Visibility.Collapsed;
    }
}
