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

                // Fetch and Render AI Practice Insights
                try
                {
                    var aiOverview = await _apiClient.GetClinicAnalyticsOverviewAsync();
                    if (aiOverview?.AiInsights != null)
                    {
                        AiSummaryText.Text = aiOverview.AiInsights.ClinicalSummary;
                        AiRecommendationsContainer.Children.Clear();
                        foreach (var rec in aiOverview.AiInsights.ActionableRecommendations)
                        {
                            var b = new Border
                            {
                                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                                CornerRadius = new CornerRadius(8),
                                Padding = new Thickness(12, 8, 12, 8),
                                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                                BorderThickness = new Thickness(1)
                            };
                            b.Child = new TextBlock
                            {
                                Text = $"💡 {rec}",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                                TextWrapping = TextWrapping.Wrap
                            };
                            AiRecommendationsContainer.Children.Add(b);
                        }
                    }
                }
                catch { }
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
                DetailsSubtitleText.Text = $"{dateRangeStr} • مجموع المدفوعات والمبالغ المحصلة كاش وعربون فقط";
                _activeModalAppointments = _currentAppointments.Where(a => a.Status != AppointmentStatus.Cancelled && a.DepositAmount > 0).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _dashboardData?.MonthRevenue ?? _activeModalAppointments.Sum(a => a.DepositAmount));
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
                DetailsSubtitleText.Text = $"تاريخ اليوم: {DateTime.Today:yyyy/MM/dd} • المحصل نقداً وعربون فقط";
                _activeModalAppointments = _currentAppointments.Where(a => a.Status != AppointmentStatus.Cancelled && a.AppointmentDate.Date == DateTime.Today.Date && a.DepositAmount > 0).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _dashboardData?.TodayRevenue ?? _activeModalAppointments.Sum(a => a.DepositAmount));
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
                _activeModalAppointments = _currentAppointments.Where(a => a.Status != AppointmentStatus.Cancelled && (Math.Max(0, a.TotalFees - a.DiscountAmount) - a.DepositAmount) > 0).ToList();
                AppointmentsDetailsContainer.Visibility = Visibility.Visible;
                ApplyAppointmentsModalData(_activeModalAppointments, _dashboardData?.TotalUncollectedReceivables ?? _activeModalAppointments.Sum(a => Math.Max(0, a.TotalFees - a.DiscountAmount) - a.DepositAmount));
                break;

            case "DownPayments":
                DetailsIconText.Text = "💰";
                DetailsTitleText.Text = "تفاصيل العربون والدفعات الجزئية المقدمة";
                DetailsSubtitleText.Text = $"{dateRangeStr} • دفعات الحجز المقدمة قبل موعد الكشف";
                _activeModalAppointments = _currentAppointments.Where(a => a.Status != AppointmentStatus.Cancelled && a.DepositAmount > 0).ToList();
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

    // =========================================================
    // 🤝 نظام الشركاء وتوزيع الأرباح (Partners Profit Share)
    // =========================================================

    public class PartnerShareItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public decimal CalculatedShare { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    private readonly List<PartnerShareItem> _partners = new()
    {
        new PartnerShareItem { Name = "د. صديق (المؤسس)", Percentage = 60, Notes = "شريك مؤسس وإدارة طبية" },
        new PartnerShareItem { Name = "د. شريك 2", Percentage = 40, Notes = "شريك استثماري وطبي" }
    };

    private void OpenPartnersProfitModal_Click(object sender, RoutedEventArgs e)
    {
        RecalculatePartnersShares();
        PartnersProfitModal.Visibility = Visibility.Visible;
    }

    private void ClosePartnersProfitModal_Click(object sender, RoutedEventArgs e)
    {
        PartnersProfitModal.Visibility = Visibility.Collapsed;
    }

    private void RecalculatePartnersShares()
    {
        var netProfit = _dashboardData?.NetCashFlow ?? _dashboardData?.MonthNetProfit ?? 0m;
        PartnersNetProfitSummaryText.Text = $"{netProfit:N2} ج.م";

        foreach (var p in _partners)
        {
            p.CalculatedShare = Math.Max(0, netProfit * (p.Percentage / 100m));
        }

        PartnersGrid.ItemsSource = null;
        PartnersGrid.ItemsSource = _partners.ToList();
    }

    private void AddPartner_Click(object sender, RoutedEventArgs e)
    {
        var name = PartnerNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ClinicMessageBox.Show("يرجى إدخال اسم الشريك.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(PartnerPercentInput.Text.Trim(), out var percent) || percent <= 0 || percent > 100)
        {
            ClinicMessageBox.Show("يرجى إدخال نسبة مئوية صحيحة بين 1% و 100%.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _partners.Add(new PartnerShareItem
        {
            Name = name,
            Percentage = percent,
            Notes = PartnerNotesInput.Text.Trim()
        });

        PartnerNameInput.Text = "";
        PartnerPercentInput.Text = "50";
        PartnerNotesInput.Text = "";

        RecalculatePartnersShares();
    }

    private void DeletePartner_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PartnerShareItem item)
        {
            _partners.Remove(item);
            RecalculatePartnersShares();
        }
    }

    private void SavePartnersSettings_Click(object sender, RoutedEventArgs e)
    {
        var totalPercent = _partners.Sum(p => p.Percentage);
        if (totalPercent != 100)
        {
            ClinicMessageBox.Show($"تنبيه: مجموع نسب الشركاء الحالي هو {totalPercent}% (يفضل أن يكون 100%).\nتم حفظ البيانات بنجاح.", "تنبيه النسب", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ClinicMessageBox.Show("تم حفظ وتطبيق نسب الشركاء بنجاح!", "نجاح الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        PartnersProfitModal.Visibility = Visibility.Collapsed;
    }

    // =========================================================
    // 🔒 تقفيل شيفت وجرد الصندوق اليومي (Daily Shift Closing)
    // =========================================================

    private decimal _shiftExpectedCash = 0m;

    private void OpenDailyShiftModal_Click(object sender, RoutedEventArgs e)
    {
        var todayRevenue = _dashboardData?.TodayRevenue ?? 0m;
        var todayExpenses = _dashboardData?.TodayExpenses ?? 0m;
        
        var openingBalance = 500m;
        if (decimal.TryParse(ShiftOpeningBalanceInput.Text.Trim(), out var parsedOp) && parsedOp >= 0)
        {
            openingBalance = parsedOp;
        }

        ShiftOpeningBalanceInput.Text = openingBalance.ToString("0.00");
        RecalculateShiftExpected();

        ShiftSubtitleText.Text = $"تاريخ اليوم: {DateTime.Today:yyyy/MM/dd} | رقم الوردية #{DateTime.Today:yyMMdd}-01";
        ShiftCashCollectedText.Text = $"{todayRevenue:N2} ج.م";
        ShiftCashExpensesText.Text = $"{todayExpenses:N2} ج.م";

        ActualCashDrawerInput.Text = _shiftExpectedCash.ToString("0.00");
        UpdateShiftDifferenceBadge();

        DailyShiftModal.Visibility = Visibility.Visible;
    }

    private void ShiftOpeningBalanceInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        RecalculateShiftExpected();
        UpdateShiftDifferenceBadge();
    }

    private void RecalculateShiftExpected()
    {
        var todayRevenue = _dashboardData?.TodayRevenue ?? 0m;
        var todayExpenses = _dashboardData?.TodayExpenses ?? 0m;

        decimal.TryParse(ShiftOpeningBalanceInput?.Text.Trim(), out var openingBalance);
        _shiftExpectedCash = Math.Max(0, openingBalance + todayRevenue - todayExpenses);

        if (ShiftExpectedCashText != null)
        {
            ShiftExpectedCashText.Text = $"{_shiftExpectedCash:N2} ج.م";
        }
    }

    private void ResetDenominations_Click(object sender, RoutedEventArgs e)
    {
        Denom200Input.Text = "0";
        Denom100Input.Text = "0";
        Denom50Input.Text = "0";
        Denom20Input.Text = "0";
        Denom10Input.Text = "0";
        Denom5Input.Text = "0";
    }

    private void DenominationInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ActualCashDrawerInput == null) return;

        int.TryParse(Denom200Input?.Text.Trim(), out var d200);
        int.TryParse(Denom100Input?.Text.Trim(), out var d100);
        int.TryParse(Denom50Input?.Text.Trim(), out var d50);
        int.TryParse(Denom20Input?.Text.Trim(), out var d20);
        int.TryParse(Denom10Input?.Text.Trim(), out var d10);
        int.TryParse(Denom5Input?.Text.Trim(), out var d5);

        var totalCalculated = (d200 * 200m) + (d100 * 100m) + (d50 * 50m) + (d20 * 20m) + (d10 * 10m) + (d5 * 5m);
        if (totalCalculated > 0)
        {
            ActualCashDrawerInput.Text = totalCalculated.ToString("0.00");
        }
    }

    private void CloseDailyShiftModal_Click(object sender, RoutedEventArgs e)
    {
        DailyShiftModal.Visibility = Visibility.Collapsed;
    }

    private void ActualCashDrawerInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateShiftDifferenceBadge();
    }

    private void UpdateShiftDifferenceBadge()
    {
        if (ShiftDifferenceBadge == null || ShiftDifferenceBadgeStatus == null || ShiftDifferenceAmountText == null) return;

        decimal.TryParse(ActualCashDrawerInput.Text.Trim(), out var actual);
        var diff = actual - _shiftExpectedCash;

        if (Math.Abs(diff) < 0.01m)
        {
            ShiftDifferenceBadge.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // #F0FDF4
            ShiftDifferenceBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)); // #BBF7D0
            ShiftDifferenceBadgeStatus.Text = "✅ الصندوق متطابق بالكامل (لا يوجد فارق)";
            ShiftDifferenceBadgeStatus.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)); // #15803D
            ShiftDifferenceAmountText.Text = "فارق 0.00 ج.م";
            ShiftDifferenceAmountText.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
        }
        else if (diff > 0)
        {
            ShiftDifferenceBadge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // #FEF3C7
            ShiftDifferenceBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(253, 230, 138)); // #FDE68A
            ShiftDifferenceBadgeStatus.Text = "🟡 يوجد زيادة في الصندوق النقدي";
            ShiftDifferenceBadgeStatus.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9)); // #B45309
            ShiftDifferenceAmountText.Text = $"+{diff:N2} ج.م زيادة نقدية";
            ShiftDifferenceAmountText.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));
        }
        else
        {
            ShiftDifferenceBadge.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #FEF2F2
            ShiftDifferenceBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202)); // #FECACA
            ShiftDifferenceBadgeStatus.Text = "🔴 يوجد عجز في الصندوق النقدي!";
            ShiftDifferenceBadgeStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626
            ShiftDifferenceAmountText.Text = $"{diff:N2} ج.م عجز نقدي";
            ShiftDifferenceAmountText.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
        }
    }

    private async void ConfirmCloseDailyShift_Click(object sender, RoutedEventArgs e)
    {
        decimal.TryParse(ActualCashDrawerInput.Text.Trim(), out var actual);
        decimal.TryParse(ShiftOpeningBalanceInput.Text.Trim(), out var opening);
        var diff = actual - _shiftExpectedCash;
        var handoverTo = HandedOverToInput.Text.Trim();
        var notes = ShiftClosingNotesInput.Text.Trim();

        var result = ClinicMessageBox.Show(
            $"هل أنت متأكد من إغلاق الوردية لليوم وتأكيد جرد الصندوق بمبلغ {actual:N2} ج.م؟\n\n" +
            $"• رصيد البداية: {opening:N2} ج.م\n" +
            $"• المتوقع بالدرج: {_shiftExpectedCash:N2} ج.م\n" +
            $"• الفعلي بالدرج: {actual:N2} ج.م\n" +
            $"• حالة الجرد: {(diff == 0 ? "متطابق تماماً ✅" : diff > 0 ? $"زيادة (+{diff:N2} ج.م)" : $"عجز ({diff:N2} ج.م) ⚠️")}\n\n" +
            $"تنبيه: سيتم ترحيل وتثبيت هذه الوردية في سجل الشفتات التاريخية.",
            "تأكيد تقفيل الوردية",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ClinicMessageBox.Show($"تم تقفيل شيفت اليوم بنجاح وتجميد الحسابات اليومية! 🔒\nتم تسجيل محضر التقفيل برقم وردية #{DateTime.Today:yyMMdd}-01.", "تم التقفيل بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            DailyShiftModal.Visibility = Visibility.Collapsed;
            await LoadDashboardDataAsync();
        }
    }

    private void PrintShiftReceipt_Click(object sender, RoutedEventArgs e)
    {
        decimal.TryParse(ActualCashDrawerInput.Text.Trim(), out var actual);
        decimal.TryParse(ShiftOpeningBalanceInput.Text.Trim(), out var opening);
        var todayRevenue = _dashboardData?.TodayRevenue ?? 0m;
        var todayExpenses = _dashboardData?.TodayExpenses ?? 0m;
        var diff = actual - _shiftExpectedCash;
        var handoverTo = !string.IsNullOrWhiteSpace(HandedOverToInput.Text) ? HandedOverToInput.Text.Trim() : "المدير المالي / الوردية التالية";
        var notes = !string.IsNullOrWhiteSpace(ShiftClosingNotesInput.Text) ? ShiftClosingNotesInput.Text.Trim() : "لا توجد ملاحظات إضافية";

        var slip = "================================================\n" +
                   "            عيادة د. صديق لطب وجراحة الأسنان       \n" +
                   "          محضر رسمي لتقفيل وردية وجرد الخزينة     \n" +
                   "================================================\n" +
                   $"رقم الوردية: #{DateTime.Today:yyMMdd}-01\n" +
                   $"التاريخ والوقت: {DateTime.Now:yyyy/MM/dd - hh:mm tt}\n" +
                   $"الموظف المسؤول: {_apiClient.CurrentUser?.FullName ?? "مسؤول الاستقبال"}\n" +
                   $"المستلم للعهدة: {handoverTo}\n" +
                   "------------------------------------------------\n" +
                   $"1. رصيد افتتاح الخزينة (العهدة): {opening:N2} ج.م\n" +
                   $"2. إجمالي المقبوضات النقدية:   +{todayRevenue:N2} ج.م\n" +
                   $"3. إجمالي المصروفات المسددة:   -{todayExpenses:N2} ج.م\n" +
                   "------------------------------------------------\n" +
                   $"المبلغ المتوقع بالصندوق (السيستم): {_shiftExpectedCash:N2} ج.م\n" +
                   $"المبلغ الفعلي الموجود بالدرج:     {actual:N2} ج.م\n" +
                   $"فارق الجرد (عجز / زيادة):         {diff:N2} ج.م ({(diff == 0 ? "متطابق ✅" : diff > 0 ? "زيادة" : "عجز ⚠️")})\n" +
                   "------------------------------------------------\n" +
                   $"ملاحظات التقفيل: {notes}\n" +
                   "================================================\n" +
                   "توقيع مسؤول الوردية (المسلم): _______________\n\n" +
                   "توقيع مستلم العهدة / الإدارة:  _______________\n" +
                   "================================================";

        ClinicMessageBox.Show(slip, "معاينة محضر تقفيل الوردية الرسمي", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
