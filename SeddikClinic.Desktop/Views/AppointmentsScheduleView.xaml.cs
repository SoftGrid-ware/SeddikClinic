using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Enums;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class AppointmentsScheduleView : UserControl
{
    private readonly ClinicApiClient _apiClient;
    private DateTime _selectedDate = DateTime.Today;
    private List<AppointmentDto> _appointments = new();
    private AppointmentDto? _reschedulingAppointment;
    private CreateAppointmentDto? _pendingBookingDto;
    private AppointmentDto? _editingAppointment;
    private List<ClinicServiceDto> _clinicServices = new();
    private readonly ObservableCollection<SelectedServiceItemDto> _currentAppointmentServices = new();
    private readonly ObservableCollection<SelectedServiceItemDto> _newBookingServices = new();

    private System.Windows.Threading.DispatcherTimer? _autoRefreshTimer;
    private int _lastAppointmentCount = -1;

    public AppointmentsScheduleView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        AppointmentDatePicker.SelectedDate = DateTime.Today;
        NewBookingDatePicker.SelectedDate = DateTime.Today;
        BookingSelectedServicesItemsControl.ItemsSource = _newBookingServices;

        Loaded += async (s, e) =>
        {
            await LoadAppointmentsDataAsync();
            StartLiveAutoRefresh();
        };

        Unloaded += (s, e) => _autoRefreshTimer?.Stop();
    }

    private void StartLiveAutoRefresh()
    {
        if (_autoRefreshTimer != null) return;
        _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoRefreshTimer.Tick += async (s, e) =>
        {
            try
            {
                var summary = await _apiClient.GetTodayAppointmentsSummaryAsync();
                if (summary != null)
                {
                    if (_lastAppointmentCount >= 0 && summary.TotalToday > _lastAppointmentCount)
                    {
                        // New booking arrived! Play sound alert
                        System.Media.SystemSounds.Asterisk.Play();
                        await LoadAppointmentsDataAsync();
                    }
                    else if (_lastAppointmentCount >= 0 && summary.TotalToday < _lastAppointmentCount)
                    {
                        await LoadAppointmentsDataAsync();
                    }
                    _lastAppointmentCount = summary.TotalToday;
                }
            }
            catch { }
        };
        _autoRefreshTimer.Start();
    }

    // إلغاء تحديد الصف عند الضغط في أي مساحة فارغة بالجدول أو الصفحة
    private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var row = FindVisualParent<DataGridRow>(dep);
            if (row == null)
            {
                AppointmentsGrid.UnselectAll();
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typed) return typed;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    public async Task LoadAppointmentsDataAsync()
    {
        try
        {
            // تحديث نص وشكل التنقل بين الأيام
            if (_selectedDate.Date == DateTime.Today)
            {
                SelectedDayDisplayText.Text = $"اليوم ({_selectedDate:yyyy/MM/dd})";
                TodayNavBtn.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                TodayNavBtn.Foreground = Brushes.White;
            }
            else
            {
                var arCulture = new CultureInfo("ar-EG");
                SelectedDayDisplayText.Text = _selectedDate.ToString("dddd، d MMMM yyyy", arCulture);
                TodayNavBtn.Background = Brushes.Transparent;
                TodayNavBtn.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }

            if (AppointmentDatePicker.SelectedDate != _selectedDate)
            {
                AppointmentDatePicker.SelectedDate = _selectedDate;
            }

            var summary = await _apiClient.GetTodayAppointmentsSummaryAsync();
            if (summary != null)
            {
                TotalTodayCountText.Text = summary.TotalScheduledToday.ToString();
                WaitingCountText.Text = summary.WaitingCount.ToString();
                InProgressCountText.Text = summary.InProgressCount.ToString();
                CompletedCountText.Text = summary.CompletedToday.ToString();
            }

            // عرض حجوزات اليوم المحدد فقط
            _appointments = await _apiClient.GetAppointmentsAsync(date: _selectedDate);
            AppointmentsGrid.ItemsSource = _appointments;

            await LoadClinicServicesCatalogAsync();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب المواعيد: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // =========================================================
    // 📅 التنقل بين الأيام على مدار السنة
    // =========================================================

    private async void PrevDay_Click(object sender, RoutedEventArgs e)
    {
        _selectedDate = _selectedDate.AddDays(-1);
        await LoadAppointmentsDataAsync();
    }

    private async void NextDay_Click(object sender, RoutedEventArgs e)
    {
        _selectedDate = _selectedDate.AddDays(1);
        await LoadAppointmentsDataAsync();
    }

    private async void TodayBtn_Click(object sender, RoutedEventArgs e)
    {
        _selectedDate = DateTime.Today;
        await LoadAppointmentsDataAsync();
    }

    private async void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppointmentDatePicker.SelectedDate.HasValue && AppointmentDatePicker.SelectedDate.Value.Date != _selectedDate.Date)
        {
            _selectedDate = AppointmentDatePicker.SelectedDate.Value.Date;
            if (IsLoaded) await LoadAppointmentsDataAsync();
        }
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadAppointmentsDataAsync();
    }

    // =========================================================
    // ➕ حجز موعد جديد مع فحص التعارض والتنبيه الشيك
    // =========================================================

    private void ToggleAddBookingPanel_Click(object sender, RoutedEventArgs e)
    {
        AddBookingModal.Visibility = AddBookingModal.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (AddBookingModal.Visibility == Visibility.Visible)
        {
            NewBookingDatePicker.SelectedDate = _selectedDate;

            // إذا كانت قائمة خدمات الحجز فارغة، يتم وضع خدمة الكشف الافتراضية
            if (!_newBookingServices.Any())
            {
                var defaultService = _clinicServices.FirstOrDefault(s => s.Name.Contains("كشف")) ?? _clinicServices.FirstOrDefault();
                if (defaultService != null)
                {
                    _newBookingServices.Add(new SelectedServiceItemDto
                    {
                        ServiceId = defaultService.Id,
                        ServiceName = defaultService.Name,
                        Price = defaultService.DefaultPrice
                    });
                    FeesInput.Text = defaultService.DefaultPrice.ToString("0");
                    if (NewBookingServicePriceInput != null)
                    {
                        NewBookingServicePriceInput.Text = defaultService.DefaultPrice.ToString("0");
                    }
                }
            }

            _ = CheckBookingConflictAsync();
        }
    }

    private async void NewBookingDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        await CheckBookingConflictAsync();
    }

    private async void AppointmentTimeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await CheckBookingConflictAsync();
    }

    private async Task CheckBookingConflictAsync()
    {
        if (BookingConflictBanner == null || NewBookingDatePicker == null || AppointmentTimeCombo == null) return;

        var date = NewBookingDatePicker.SelectedDate ?? _selectedDate;
        var selectedItem = AppointmentTimeCombo.SelectedItem as ComboBoxItem;
        if (selectedItem == null) return;

        var timeTag = selectedItem.Tag?.ToString() ?? "14:00";
        var timeText = selectedItem.Content?.ToString() ?? "02:00 م";

        try
        {
            List<AppointmentDto> dateAppointments;
            if (date.Date == _selectedDate.Date)
            {
                dateAppointments = _appointments;
            }
            else
            {
                dateAppointments = await _apiClient.GetAppointmentsAsync(date: date);
            }

            var conflict = dateAppointments.FirstOrDefault(a => a.Status != AppointmentStatus.Cancelled &&
                (a.StartTimeFormatted.Contains(timeText.Replace(" ", "")) || a.StartTime.ToString(@"hh\:mm") == timeTag));

            if (conflict != null)
            {
                BookingConflictBannerText.Text = $"المريض: {conflict.PatientName} ({conflict.PatientPhone}) • الموعد: {timeText} • الخدمة: {conflict.ServiceType}";
                BookingConflictBanner.Visibility = Visibility.Visible;
            }
            else
            {
                BookingConflictBanner.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            BookingConflictBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void ServiceTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NewBookingServicePriceInput != null && ServiceTypeCombo?.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            if (decimal.TryParse(item.Tag.ToString(), out var price) && price >= 0)
            {
                NewBookingServicePriceInput.Text = price.ToString("0");
            }
        }
    }

    private void AddBookingServiceItem_Click(object sender, RoutedEventArgs e)
    {
        var serviceName = (ServiceTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            ClinicMessageBox.Show("يرجى اختيار خدمة طبية لإضافتها للحجز.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(NewBookingServicePriceInput.Text.Trim(), out var price);

        _newBookingServices.Add(new SelectedServiceItemDto
        {
            ServiceName = serviceName,
            Price = price
        });

        FeesInput.Text = _newBookingServices.Sum(s => s.Price).ToString("0");
        UpdateBookingRemaining();
    }

    private void RemoveBookingServiceItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SelectedServiceItemDto item)
        {
            _newBookingServices.Remove(item);
            FeesInput.Text = _newBookingServices.Sum(s => s.Price).ToString("0");
            UpdateBookingRemaining();
        }
    }

    private void EnableInstallmentCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (EnableInstallmentCheckBox == null || InstallmentDetailsPanel == null || PaymentTypeBadge == null || PaymentTypeBadgeText == null || InstallmentModeContainer == null) return;

        var isInstallment = EnableInstallmentCheckBox.IsChecked == true;
        if (isInstallment)
        {
            InstallmentDetailsPanel.Visibility = Visibility.Visible;
            PaymentTypeBadge.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)); // #EFF6FF
            PaymentTypeBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)); // #BFDBFE
            PaymentTypeBadgeText.Text = "💳 نظام التقسيط مفعل";
            PaymentTypeBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // #2563EB
            InstallmentModeContainer.Background = new SolidColorBrush(Color.FromRgb(240, 249, 255)); // #F0F9FF
            InstallmentModeContainer.BorderBrush = new SolidColorBrush(Color.FromRgb(186, 230, 253)); // #BAE6FD

            if (DepositInput != null && FeesInput != null && DepositInput.Text == FeesInput.Text)
            {
                DepositInput.Text = "0";
            }
        }
        else
        {
            InstallmentDetailsPanel.Visibility = Visibility.Collapsed;
            PaymentTypeBadge.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // #DCFCE7
            PaymentTypeBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)); // #BBF7D0
            PaymentTypeBadgeText.Text = "🟢 سداد كاش بالكامل";
            PaymentTypeBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)); // #15803D
            InstallmentModeContainer.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)); // #F8FAFC
            InstallmentModeContainer.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #E2E8F0

            if (DepositInput != null && FeesInput != null)
            {
                DepositInput.Text = FeesInput.Text;
            }
        }

        UpdateBookingRemaining();
    }

    private void BookingFinancialInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateBookingRemaining();
    }

    private void UpdateBookingRemaining()
    {
        if (FeesInput == null || DepositInput == null || BookingRemainingBadgeText == null) return;

        decimal.TryParse(FeesInput.Text.Trim(), out var fees);
        decimal.TryParse(DepositInput.Text.Trim(), out var deposit);
        var remaining = Math.Max(0, fees - deposit);

        BookingRemainingBadgeText.Text = $"{remaining:N0} ج.م";
    }

    private async void ConfirmBooking_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PatientNameInput.Text))
        {
            ClinicMessageBox.Show("يرجى إدخال اسم المريض.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PatientPhoneInput.Text))
        {
            ClinicMessageBox.Show("يرجى إدخال رقم هاتف المريض.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(FeesInput.Text, out var fees);
        var totalFees = fees > 0 ? fees : 250m;
        decimal deposit;

        if (EnableInstallmentCheckBox?.IsChecked == true)
        {
            decimal.TryParse(DepositInput.Text, out deposit);
        }
        else
        {
            deposit = totalFees; // كاش بالكامل
        }

        var bookingDate = NewBookingDatePicker.SelectedDate ?? _selectedDate;
        var selectedItem = AppointmentTimeCombo.SelectedItem as ComboBoxItem;
        var timeTag = selectedItem?.Tag?.ToString() ?? "14:00";
        var timeText = selectedItem?.Content?.ToString() ?? "02:00 م";

        var combinedService = _newBookingServices.Any()
            ? string.Join(" + ", _newBookingServices.Select(s => s.ServiceName))
            : ((ServiceTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "كشف واستشارة طبية");

        var dto = new CreateAppointmentDto
        {
            NewPatientFullName = PatientNameInput.Text.Trim(),
            NewPatientPhone = PatientPhoneInput.Text.Trim(),
            AppointmentDate = bookingDate,
            StartTimeString = timeTag,
            DurationMinutes = 30,
            ServiceType = combinedService,
            TotalFees = totalFees,
            DepositAmount = deposit
        };

        // فحص وجود تعارض في نفس الموعد والتاريخ
        List<AppointmentDto> dateAppointments;
        if (bookingDate.Date == _selectedDate.Date)
        {
            dateAppointments = _appointments;
        }
        else
        {
            dateAppointments = await _apiClient.GetAppointmentsAsync(date: bookingDate);
        }

        var conflict = dateAppointments.FirstOrDefault(a => a.Status != AppointmentStatus.Cancelled &&
            (a.StartTimeFormatted.Contains(timeText.Replace(" ", "")) || a.StartTime.ToString(@"hh\:mm") == timeTag));

        if (conflict != null)
        {
            // إظهار التنبيه الشيك الأنيق
            _pendingBookingDto = dto;
            ConflictDetailsText.Text = $"يوجد حجز مسجل مسبقاً في هذا التوقيت ({timeText}) بتاريخ ({bookingDate:yyyy/MM/dd}):\n• المريض: {conflict.PatientName} (هاتف: {conflict.PatientPhone})\n• الخدمة: {conflict.ServiceType}";
            ConflictWarningModal.Visibility = Visibility.Visible;
            return;
        }

        // لا يوجد تعارض - المتابعة بالحفظ مباشرة
        await ExecuteCreateBookingAsync(dto);
    }

    private async void ConfirmConflictBooking_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingBookingDto != null)
        {
            ConflictWarningModal.Visibility = Visibility.Collapsed;
            var dto = _pendingBookingDto;
            _pendingBookingDto = null;
            await ExecuteCreateBookingAsync(dto);
        }
    }

    private void CancelConflict_Click(object sender, RoutedEventArgs e)
    {
        ConflictWarningModal.Visibility = Visibility.Collapsed;
        _pendingBookingDto = null;
    }

    private async Task ExecuteCreateBookingAsync(CreateAppointmentDto dto)
    {
        try
        {
            await _apiClient.CreateAppointmentAsync(dto);
            ClinicMessageBox.Show("تم حجز وتأكيد الموعد بنجاح!", "نجاح الحجز", MessageBoxButton.OK, MessageBoxImage.Information);

            PatientNameInput.Text = "";
            PatientPhoneInput.Text = "";
            _newBookingServices.Clear();
            if (EnableInstallmentCheckBox != null) EnableInstallmentCheckBox.IsChecked = false;
            BookingConflictBanner.Visibility = Visibility.Collapsed;
            AddBookingModal.Visibility = Visibility.Collapsed;

            // إذا كان الموعد في يوم آخر، الانتقال إليه لرؤيته فوراً
            if (dto.AppointmentDate.Date != _selectedDate.Date)
            {
                _selectedDate = dto.AppointmentDate.Date;
            }

            await LoadAppointmentsDataAsync();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"فشل الحجز: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================
    // 🕒 تعديل وتغيير موعد وتاريخ الحجز (Reschedule)
    // =========================================================

    private void Reschedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AppointmentDto apt)
        {
            _reschedulingAppointment = apt;
            ReschedulePatientNameText.Text = $"للمريض: {apt.PatientName}";
            RescheduleDatePicker.SelectedDate = apt.AppointmentDate;

            // تحديد وقت الموعد الحالي في القائمة
            var aptTimeStr = apt.StartTime.ToString(@"hh\:mm");
            foreach (ComboBoxItem item in RescheduleTimeCombo.Items)
            {
                if (item.Tag?.ToString() == aptTimeStr)
                {
                    RescheduleTimeCombo.SelectedItem = item;
                    break;
                }
            }

            RescheduleModal.Visibility = Visibility.Visible;
        }
    }

    private async void SaveReschedule_Click(object sender, RoutedEventArgs e)
    {
        if (_reschedulingAppointment == null) return;

        var newDate = RescheduleDatePicker.SelectedDate ?? DateTime.Today;
        var selectedItem = RescheduleTimeCombo.SelectedItem as ComboBoxItem;
        var newTimeTag = selectedItem?.Tag?.ToString() ?? "14:00";
        var newTimeText = selectedItem?.Content?.ToString() ?? "02:00 م";

        try
        {
            var success = await _apiClient.RescheduleAppointmentAsync(_reschedulingAppointment.Id, newDate, newTimeTag);
            if (success)
            {
                RescheduleModal.Visibility = Visibility.Collapsed;

                if (newDate.Date != _selectedDate.Date)
                {
                    _selectedDate = newDate.Date;
                }

                await LoadAppointmentsDataAsync();
                ClinicMessageBox.Show($"تم تعديل موعد الحجز بنجاح إلى:\nالتاريخ: {newDate:yyyy/MM/dd}\nالساعة: {newTimeText}", "تم تعديل الموعد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ClinicMessageBox.Show("تعذر تعديل موعد الحجز.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"حدث خطأ أثناء تعديل الموعد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelReschedule_Click(object sender, RoutedEventArgs e)
    {
        RescheduleModal.Visibility = Visibility.Collapsed;
    }

    // =========================================================
    // ⚡ تحديث حالات الموعد السريعة (انتظار، كشف، إنهاء، إلغاء، حذف)
    // =========================================================

    private AppointmentDto? GetAppointmentFromSender(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is AppointmentDto dto)
            return dto;
        if (sender is MenuItem mi && mi.DataContext is AppointmentDto miDto)
            return miDto;
        return null;
    }

    private void OpenActionsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void ViewNotes_Click(object sender, RoutedEventArgs e)
    {
        var apt = GetAppointmentFromSender(sender);
        if (apt == null) return;

        NotesModalPatientInfo.Text = $"المريض: {apt.PatientName} ({apt.PatientPhone}) | رقم الحجز: {apt.AppointmentNumber}";
        NotesModalContentText.Text = !string.IsNullOrWhiteSpace(apt.Notes)
            ? apt.Notes
            : (!string.IsNullOrWhiteSpace(apt.ReasonForVisit) ? apt.ReasonForVisit : "لا توجد ملاحظات مسجلة لهذا الحجز.");
        PatientNotesModal.Visibility = Visibility.Visible;
    }

    private void CloseNotesModal_Click(object sender, RoutedEventArgs e)
    {
        PatientNotesModal.Visibility = Visibility.Collapsed;
    }

    private async void SetWaiting_Click(object sender, RoutedEventArgs e)
    {
        var apt = GetAppointmentFromSender(sender);
        if (apt != null)
        {
            await _apiClient.UpdateAppointmentStatusAsync(apt.Id, AppointmentStatus.Waiting);
            await LoadAppointmentsDataAsync();
        }
    }

    private async void SetInProgress_Click(object sender, RoutedEventArgs e)
    {
        var apt = GetAppointmentFromSender(sender);
        if (apt != null)
        {
            await _apiClient.UpdateAppointmentStatusAsync(apt.Id, AppointmentStatus.InProgress);
            await LoadAppointmentsDataAsync();
        }
    }

    private async void SetCompleted_Click(object sender, RoutedEventArgs e)
    {
        var apt = GetAppointmentFromSender(sender);
        if (apt != null)
        {
            await _apiClient.UpdateAppointmentStatusAsync(apt.Id, AppointmentStatus.Completed);
            await LoadAppointmentsDataAsync();
        }
    }

    private AppointmentDto? _cancelingAppointment;

    private void CancelBooking_Click(object sender, RoutedEventArgs e)
    {
        var apt = GetAppointmentFromSender(sender);
        if (apt != null)
        {
            _cancelingAppointment = apt;
            CancelModalPatientText.Text = $"المريض: {apt.PatientName} ({apt.PatientPhone}) | رقم الحجز: {apt.AppointmentNumber}";
            CancellationReasonInput.Text = "اعتذار الطبيب لظرف طارئ";
            CancelAppointmentModal.Visibility = Visibility.Visible;
            CancellationReasonInput.Focus();
        }
    }

    private void CloseCancelModal_Click(object sender, RoutedEventArgs e)
    {
        CancelAppointmentModal.Visibility = Visibility.Collapsed;
        _cancelingAppointment = null;
    }

    private async void ConfirmCancelWithReason_Click(object sender, RoutedEventArgs e)
    {
        if (_cancelingAppointment == null) return;

        var reason = CancellationReasonInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "تم إلغاء الموعد بواسطة إدارة العيادة";
        }

        try
        {
            var success = await _apiClient.UpdateAppointmentStatusAsync(_cancelingAppointment.Id, AppointmentStatus.Cancelled, reason);
            if (success)
            {
                CancelAppointmentModal.Visibility = Visibility.Collapsed;
                _cancelingAppointment = null;
                await LoadAppointmentsDataAsync();
                ClinicMessageBox.Show($"تم إلغاء الحجز بنجاح وإرسال سبب الإلغاء للمريض:\n({reason})", "تم الإلغاء", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ClinicMessageBox.Show("تعذر إلغاء الحجز.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"حدث خطأ أثناء الإلغاء: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintInvoice_Click(object sender, RoutedEventArgs e)
    {
        var apt = GetAppointmentFromSender(sender);
        if (apt == null) return;

        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                var remaining = apt.TotalFees - apt.DepositAmount;
                if (remaining < 0) remaining = 0;

                var invoiceCard = new Border
                {
                    Width = 650,
                    Padding = new Thickness(35),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(12),
                    FlowDirection = FlowDirection.RightToLeft
                };

                var rootStack = new StackPanel();

                // Header
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var titleStack = new StackPanel();
                titleStack.Children.Add(new TextBlock { Text = "عيادة د. صديق التخصصية", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
                titleStack.Children.Add(new TextBlock { Text = "طب وجراحة وزراعة الأسنان", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 2, 0, 0) });
                titleStack.Children.Add(new TextBlock { Text = "فاتورة كشف طبي وسند خدمات", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Margin = new Thickness(0, 8, 0, 0) });
                Grid.SetColumn(titleStack, 0);
                headerGrid.Children.Add(titleStack);

                var badgeBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(240, 249, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(186, 230, 253)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 8, 14, 8)
                };
                var badgeStack = new StackPanel();
                badgeStack.Children.Add(new TextBlock { Text = $"رقم الفاتورة: {apt.AppointmentNumber}", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)) });
                badgeStack.Children.Add(new TextBlock { Text = $"التاريخ: {apt.AppointmentDate:yyyy/MM/dd}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 2, 0, 0) });
                badgeBorder.Child = badgeStack;
                Grid.SetColumn(badgeBorder, 1);
                headerGrid.Children.Add(badgeBorder);

                rootStack.Children.Add(headerGrid);

                // Divider
                rootStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 18) });

                // Patient Details
                var detailsBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 0, 20)
                };
                var detailsGrid = new Grid();
                detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var pName = new TextBlock { Text = $"اسم المريض: {apt.PatientName}", FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) };
                Grid.SetRow(pName, 0); Grid.SetColumn(pName, 0); detailsGrid.Children.Add(pName);

                var pPhone = new TextBlock { Text = $"رقم الهاتف: {apt.PatientPhone}", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)) };
                Grid.SetRow(pPhone, 0); Grid.SetColumn(pPhone, 1); detailsGrid.Children.Add(pPhone);

                var pTime = new TextBlock { Text = $"توقيت الكشف: {apt.FormattedTime}", FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 6, 0, 0) };
                Grid.SetRow(pTime, 1); Grid.SetColumn(pTime, 0); detailsGrid.Children.Add(pTime);

                var pDoctor = new TextBlock { Text = $"الطبيب المعالج: {apt.DoctorName}", FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Margin = new Thickness(0, 6, 0, 0) };
                Grid.SetRow(pDoctor, 1); Grid.SetColumn(pDoctor, 1); detailsGrid.Children.Add(pDoctor);

                detailsBorder.Child = detailsGrid;
                rootStack.Children.Add(detailsBorder);

                // Service Details Table
                var tableBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 20)
                };
                var tableStack = new StackPanel();

                var tableHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    Padding = new Thickness(14, 8, 14, 8)
                };
                var thGrid = new Grid();
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
                thGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var th1 = new TextBlock { Text = "البيان / الخدمات الطبية", FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)) };
                var th2 = new TextBlock { Text = "القيمة والرسوم", FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), TextAlignment = TextAlignment.Left };
                Grid.SetColumn(th1, 0); thGrid.Children.Add(th1);
                Grid.SetColumn(th2, 1); thGrid.Children.Add(th2);
                tableHeader.Child = thGrid;
                tableStack.Children.Add(tableHeader);

                var tableRow = new Border { Padding = new Thickness(14, 12, 14, 12) };
                var trGrid = new Grid();
                trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
                trGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var tr1 = new TextBlock { Text = apt.ServiceType, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), TextWrapping = TextWrapping.Wrap };
                var tr2 = new TextBlock { Text = $"{apt.TotalFees:N0} ج.م", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)), TextAlignment = TextAlignment.Left };
                Grid.SetColumn(tr1, 0); trGrid.Children.Add(tr1);
                Grid.SetColumn(tr2, 1); trGrid.Children.Add(tr2);
                tableRow.Child = trGrid;
                tableStack.Children.Add(tableRow);

                tableBorder.Child = tableStack;
                rootStack.Children.Add(tableBorder);

                // Financial Totals
                var totalsStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) };
                totalsStack.Children.Add(new TextBlock { Text = $"إجمالي الرسوم: {apt.TotalFees:N0} ج.م", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
                totalsStack.Children.Add(new TextBlock { Text = $"المسدد: {apt.DepositAmount:N0} ج.م", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
                totalsStack.Children.Add(new TextBlock { Text = $"المتبقي: {remaining:N0} ج.م", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0) });
                rootStack.Children.Add(totalsStack);

                // Footer
                rootStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 14) });
                var footerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                footerStack.Children.Add(new TextBlock { Text = "هاتف وحجوزات العيادة: 01126092725 | فيسبوك: facebook.com/SeddikDentalClinic", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), HorizontalAlignment = HorizontalAlignment.Center });
                footerStack.Children.Add(new TextBlock { Text = "مع تمنياتنا لكم بدوام الصحة والعافية والابتسامة الجميلة ✨", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
                rootStack.Children.Add(footerStack);

                invoiceCard.Child = rootStack;

                invoiceCard.Measure(new Size(printDlg.PrintableAreaWidth, printDlg.PrintableAreaHeight));
                invoiceCard.Arrange(new Rect(new Point(20, 20), invoiceCard.DesiredSize));

                printDlg.PrintVisual(invoiceCard, $"فاتورة_عيادة_صديق_{apt.PatientName}");
                ClinicMessageBox.Show("تم إرسال الفاتورة إلى أمر الطباعة بنجاح!", "طباعة الفاتورة", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"تعذر إتمام الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteBooking_Click(object sender, RoutedEventArgs e)
    {
        var apt = GetAppointmentFromSender(sender);
        if (apt != null)
        {
            var result = ClinicMessageBox.Show($"هل أنت متأكد من حذف هذا الموعد نهائياً للمريض: {apt.PatientName}؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                var success = await _apiClient.DeleteAppointmentAsync(apt.Id);
                if (success)
                {
                    await LoadAppointmentsDataAsync();
                }
                else
                {
                    ClinicMessageBox.Show("تعذر حذف الموعد.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // =========================================================
    // 🩺 إدارة وتعديل الخدمات الطبية للحالة (Multi-Service Support)
    // =========================================================

    private async Task LoadClinicServicesCatalogAsync()
    {
        try
        {
            _clinicServices = await _apiClient.GetClinicServicesAsync();

            if (ServiceTypeCombo != null && _clinicServices.Any())
            {
                ServiceTypeCombo.Items.Clear();
                foreach (var s in _clinicServices)
                {
                    ServiceTypeCombo.Items.Add(new ComboBoxItem { Content = s.Name, Tag = s.DefaultPrice });
                }
                ServiceTypeCombo.SelectedIndex = 0;
            }
        }
        catch
        {
            // fallback
        }
    }

    private void SetupPresetServices()
    {
        PresetServicesCombo.Items.Clear();
        foreach (var s in _clinicServices)
        {
            PresetServicesCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{s.Name} ({s.DefaultPrice:N0} ج.م)",
                Tag = $"{s.Name}|{s.DefaultPrice}"
            });
        }

        if (PresetServicesCombo.Items.Count > 0)
        {
            PresetServicesCombo.SelectedIndex = 0;
        }
    }

    private async void ChangeService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AppointmentDto apt)
        {
            _editingAppointment = apt;

            if (!_clinicServices.Any())
            {
                await LoadClinicServicesCatalogAsync();
            }

            SetupPresetServices();
            _currentAppointmentServices.Clear();

            var currentServices = apt.ServiceType.Split(new[] { " + ", "+", "،", "," }, StringSplitOptions.RemoveEmptyEntries);
            if (currentServices.Length > 0)
            {
                foreach (var rawName in currentServices)
                {
                    var name = rawName.Trim();
                    var matched = _clinicServices.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    var price = matched?.DefaultPrice ?? (currentServices.Length == 1 ? apt.TotalFees : 0m);

                    _currentAppointmentServices.Add(new SelectedServiceItemDto
                    {
                        ServiceId = matched?.Id,
                        ServiceName = name,
                        Price = price
                    });
                }
            }
            else
            {
                _currentAppointmentServices.Add(new SelectedServiceItemDto
                {
                    ServiceName = "كشف واستشارة طبية",
                    Price = apt.TotalFees > 0 ? apt.TotalFees : 250m
                });
            }

            SelectedServicesItemsControl.ItemsSource = _currentAppointmentServices;
            EditServicePatientNameText.Text = $"للمريض: {apt.PatientName}";
            EditServiceFeesInput.Text = apt.TotalFees > 0 ? apt.TotalFees.ToString("0") : _currentAppointmentServices.Sum(s => s.Price).ToString("0");

            EditServiceModal.Visibility = Visibility.Visible;
        }
    }

    private void PresetServicesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetServicesCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var parts = tag.Split('|');
            if (parts.Length == 2)
            {
                NewServiceItemPriceInput.Text = parts[1];
            }
        }
    }

    private void AddServiceItem_Click(object sender, RoutedEventArgs e)
    {
        string serviceName = "";
        if (PresetServicesCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var parts = tag.Split('|');
            serviceName = parts[0];
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            ClinicMessageBox.Show("يرجى اختيار الخدمة الطبية المراد إضافتها.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(NewServiceItemPriceInput.Text.Trim(), out var price);

        _currentAppointmentServices.Add(new SelectedServiceItemDto
        {
            ServiceName = serviceName,
            Price = price
        });

        EditServiceFeesInput.Text = _currentAppointmentServices.Sum(s => s.Price).ToString("0");
    }

    private void RemoveServiceItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SelectedServiceItemDto item)
        {
            _currentAppointmentServices.Remove(item);
            EditServiceFeesInput.Text = _currentAppointmentServices.Sum(s => s.Price).ToString("0");
        }
    }

    private async void SaveServiceChange_Click(object sender, RoutedEventArgs e)
    {
        if (_editingAppointment == null) return;

        if (!_currentAppointmentServices.Any())
        {
            ClinicMessageBox.Show("يرجى إضافة خدمة طبية واحدة على الأقل للحالة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var combinedServices = string.Join(" + ", _currentAppointmentServices.Select(s => s.ServiceName));
        decimal.TryParse(EditServiceFeesInput.Text.Trim(), out var newFees);

        try
        {
            var success = await _apiClient.UpdateAppointmentServiceAsync(_editingAppointment.Id, combinedServices, newFees);
            if (success)
            {
                EditServiceModal.Visibility = Visibility.Collapsed;
                await LoadAppointmentsDataAsync();
                ClinicMessageBox.Show($"تم حفظ الخدمات بنجاح!\nالخدمات: {combinedServices}\nالرسوم الإجمالية: {newFees:N0} ج.م", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ClinicMessageBox.Show("تعذر تعديل الخدمات.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء التعديل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelServiceChange_Click(object sender, RoutedEventArgs e)
    {
        EditServiceModal.Visibility = Visibility.Collapsed;
    }

    private async void OpenWorkingHoursModal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = await _apiClient.GetWorkingHoursAsync();
            if (config != null)
            {
                WhStartTimeInput.Text = config.StartTime;
                WhEndTimeInput.Text = config.EndTime;
                WhDurationInput.Text = config.SlotDurationMinutes.ToString();
                WhDaysInput.Text = config.ClinicDays;
            }
        }
        catch { }

        WorkingHoursModal.Visibility = Visibility.Visible;
    }

    private void CloseWorkingHoursModal_Click(object sender, RoutedEventArgs e)
    {
        WorkingHoursModal.Visibility = Visibility.Collapsed;
    }

    private async void SaveWorkingHoursModal_Click(object sender, RoutedEventArgs e)
    {
        int.TryParse(WhDurationInput.Text.Trim(), out var duration);
        var dto = new SeddikClinic.Core.DTOs.Settings.WorkingHoursConfigDto
        {
            StartTime = WhStartTimeInput.Text.Trim(),
            EndTime = WhEndTimeInput.Text.Trim(),
            SlotDurationMinutes = duration > 0 ? duration : 30,
            ClinicDays = WhDaysInput.Text.Trim()
        };

        var ok = await _apiClient.UpdateWorkingHoursAsync(dto);
        if (ok)
        {
            WorkingHoursModal.Visibility = Visibility.Collapsed;
            ClinicMessageBox.Show("تم حفظ وتحديث مواعيد العمل بنجاح ومزامنتها مع كافة تطبيقات الهاتف!", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ClinicMessageBox.Show("تعذر حفظ مواعيد العمل. يرجى المحاولة مرة أخرى.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
