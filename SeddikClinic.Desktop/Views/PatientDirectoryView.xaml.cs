using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class PatientDirectoryView : UserControl
{
    private readonly ClinicApiClient _apiClient;
    private List<PatientDto> _patientsList = new();
    private PatientDto? _editingPatient;
    private PatientDto? _currentModalPatient;

    public PatientDirectoryView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        Loaded += async (s, e) => await LoadPatientsAsync();
    }

    // إلغاء تحديد الصف عند الضغط في أي مكان فارغ
    private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var row = FindVisualParent<DataGridRow>(dep);
            if (row == null)
            {
                PatientsGrid.UnselectAll();
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

    public async Task LoadPatientsAsync()
    {
        try
        {
            var query = SearchBox.Text.Trim();
            _patientsList = await _apiClient.SearchPatientsAsync(string.IsNullOrEmpty(query) ? null : query);
            PatientsGrid.ItemsSource = _patientsList;

            // تحديث كروت الإحصائيات السريعة
            TotalPatientsCountText.Text = _patientsList.Count.ToString();
            VisitedPatientsCountText.Text = _patientsList.Count(p => p.TotalVisits > 0).ToString();
            NewPatientsThisMonthCountText.Text = _patientsList.Count(p => p.CreatedAt.Month == DateTime.Today.Month && p.CreatedAt.Year == DateTime.Today.Year).ToString();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب سجل المرضى: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleAddPatientPanel_Click(object sender, RoutedEventArgs e)
    {
        AddPatientPanel.Visibility = AddPatientPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void SavePatient_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FullNameInput.Text))
        {
            ClinicMessageBox.Show("يرجى إدخال اسم المريض.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PhoneInput.Text))
        {
            ClinicMessageBox.Show("يرجى إدخال رقم هاتف المريض.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int? age = int.TryParse(AgeInput.Text, out var a) ? a : null;

        try
        {
            var dto = new CreatePatientDto
            {
                FullName = FullNameInput.Text.Trim(),
                PhoneNumber = PhoneInput.Text.Trim(),
                Gender = (GenderCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ذكر",
                Age = age,
                MedicalHistory = MedicalHistoryInput.Text.Trim(),
                Allergies = AllergiesInput.Text.Trim()
            };

            await _apiClient.CreatePatientAsync(dto);
            ClinicMessageBox.Show("تم تسجيل ملف المريض بنجاح!", "نجاح التسجيل", MessageBoxButton.OK, MessageBoxImage.Information);

            FullNameInput.Text = "";
            PhoneInput.Text = "";
            AgeInput.Text = "";
            MedicalHistoryInput.Text = "";
            AllergiesInput.Text = "";
            AddPatientPanel.Visibility = Visibility.Collapsed;

            await LoadPatientsAsync();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"فشل تسجيل المريض: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================
    // 📋 عرض السجل الطبي وتاريخ الزيارات الكامل للمريض
    // =========================================================

    private async void ViewMedicalRecord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientDto patientSummary)
        {
            try
            {
                // جلب ملف المريض المفصل شاملاً الزيارات والمواعيد السابقة
                var fullPatient = await _apiClient.GetPatientByIdAsync(patientSummary.Id);
                if (fullPatient == null) fullPatient = patientSummary;
                _currentModalPatient = fullPatient;

                ModalPatientSubheaderText.Text = $"ملف المريض: {fullPatient.FullName}";
                ModalPatientCodeText.Text = fullPatient.PatientCode;
                ModalPatientPhoneText.Text = fullPatient.PhoneNumber;
                ModalPatientAgeGenderText.Text = $"{fullPatient.Age?.ToString() ?? "-"} سنة ({fullPatient.Gender})";
                ModalPatientVisitsCountText.Text = $"{fullPatient.Visits.Count} زيارة";

                ModalPatientAllergiesText.Text = !string.IsNullOrWhiteSpace(fullPatient.Allergies) 
                    ? fullPatient.Allergies 
                    : "لا توجد حساسية مسجلة";

                ModalPatientHistoryText.Text = !string.IsNullOrWhiteSpace(fullPatient.MedicalHistory) 
                    ? fullPatient.MedicalHistory 
                    : "سليم - لا توجد أمراض مزمنة مسجلة";

                VisitsHistoryGrid.ItemsSource = fullPatient.Visits;
                MedicalRecordModal.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ClinicMessageBox.Show($"خطأ أثناء فتح السجل الطبي: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseMedicalRecordModal_Click(object sender, RoutedEventArgs e)
    {
        MedicalRecordModal.Visibility = Visibility.Collapsed;
        _currentModalPatient = null;
    }

    // =========================================================
    // ✏️ تعديل التاريخ المرضي والحساسية (ضغط، سكر، حساسية...)
    // =========================================================

    private void EditMedicalHistory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientDto patient)
        {
            OpenEditMedicalHistoryModal(patient);
        }
    }

    private void EditMedicalHistoryFromModal_Click(object sender, RoutedEventArgs e)
    {
        if (_currentModalPatient != null)
        {
            OpenEditMedicalHistoryModal(_currentModalPatient);
        }
    }

    private void OpenEditMedicalHistoryModal(PatientDto patient)
    {
        _editingPatient = patient;
        EditModalPatientNameText.Text = patient.FullName;
        EditModalPatientCodeText.Text = patient.PatientCode;
        EditModalPatientPhoneText.Text = patient.PhoneNumber;
        EditHistoryPatientSubtitle.Text = $"المريض: {patient.FullName} | كود: {patient.PatientCode}";

        EditModalMedicalHistoryInput.Text = !string.IsNullOrWhiteSpace(patient.MedicalHistory) ? patient.MedicalHistory : "";
        EditModalAllergiesInput.Text = !string.IsNullOrWhiteSpace(patient.Allergies) ? patient.Allergies : "";

        EditMedicalHistoryModal.Visibility = Visibility.Visible;
    }

    private void CloseEditMedicalHistoryModal_Click(object sender, RoutedEventArgs e)
    {
        EditMedicalHistoryModal.Visibility = Visibility.Collapsed;
        _editingPatient = null;
    }

    private void QuickAddHistory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            var current = EditModalMedicalHistoryInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(current) || current.Contains("سليم"))
            {
                EditModalMedicalHistoryInput.Text = tag;
            }
            else if (!current.Contains(tag))
            {
                EditModalMedicalHistoryInput.Text = $"{current} + {tag}";
            }
        }
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        EditModalMedicalHistoryInput.Text = "سليم - لا توجد أمراض مزمنة";
    }

    private void QuickAddAllergy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            var current = EditModalAllergiesInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(current) || current.Contains("لا توجد"))
            {
                EditModalAllergiesInput.Text = tag;
            }
            else if (!current.Contains(tag))
            {
                EditModalAllergiesInput.Text = $"{current} + {tag}";
            }
        }
    }

    private void ClearAllergies_Click(object sender, RoutedEventArgs e)
    {
        EditModalAllergiesInput.Text = "لا توجد حساسية مسجلة";
    }

    private async void SaveMedicalHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_editingPatient == null) return;

        try
        {
            var newHistory = EditModalMedicalHistoryInput.Text.Trim();
            var newAllergies = EditModalAllergiesInput.Text.Trim();

            var updateDto = new CreatePatientDto
            {
                FullName = _editingPatient.FullName,
                PhoneNumber = _editingPatient.PhoneNumber,
                AlternativePhone = _editingPatient.AlternativePhone,
                NationalId = _editingPatient.NationalId,
                Gender = _editingPatient.Gender,
                Age = _editingPatient.Age,
                BirthDate = _editingPatient.BirthDate,
                Address = _editingPatient.Address,
                BloodGroup = _editingPatient.BloodGroup,
                MedicalHistory = newHistory,
                Allergies = newAllergies,
                Notes = _editingPatient.Notes
            };

            var updated = await _apiClient.UpdatePatientAsync(_editingPatient.Id, updateDto);
            if (updated != null)
            {
                _editingPatient.MedicalHistory = newHistory;
                _editingPatient.Allergies = newAllergies;

                if (_currentModalPatient != null && _currentModalPatient.Id == _editingPatient.Id)
                {
                    _currentModalPatient.MedicalHistory = newHistory;
                    _currentModalPatient.Allergies = newAllergies;
                    ModalPatientHistoryText.Text = !string.IsNullOrWhiteSpace(newHistory) ? newHistory : "سليم - لا توجد أمراض مزمنة مسجلة";
                    ModalPatientAllergiesText.Text = !string.IsNullOrWhiteSpace(newAllergies) ? newAllergies : "لا توجد حساسية مسجلة";
                }

                EditMedicalHistoryModal.Visibility = Visibility.Collapsed;
                await LoadPatientsAsync();

                ClinicMessageBox.Show($"تم تحديث التاريخ المرضي وتنبيهات الحساسية للمريض '{_editingPatient.FullName}' بنجاح!", "تم التحديث بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ClinicMessageBox.Show("تعذر تحديث البيانات الطبية للمريض.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء حفظ التعديلات الطبية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================
    // 🗑️ حذف المريض من السجل
    // =========================================================

    private async void DeletePatient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientDto patient)
        {
            var result = ClinicMessageBox.Show(
                $"هل أنت متأكد من حذف المريض '{patient.FullName}' (كود: {patient.PatientCode}) من السجل؟\n\nتنبيه: سيتم إخفاء هذا المريض وسجلاته المرتبطة من المنظومة.",
                "تأكيد حذف المريض",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = await _apiClient.DeletePatientAsync(patient.Id);
                    if (success)
                    {
                        await LoadPatientsAsync();
                        ClinicMessageBox.Show($"تم حذف المريض '{patient.FullName}' بنجاح.", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ClinicMessageBox.Show("تعذر حذف المريض.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    ClinicMessageBox.Show($"خطأ أثناء الحذف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) await LoadPatientsAsync();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadPatientsAsync();
    }

    // =========================================================
    // 💵 سداد وتحصيل قسط / دفعة مالية
    // =========================================================

    private PatientVisitHistoryDto? _selectedVisitForPayment;

    private void PayInstallment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientVisitHistoryDto visit)
        {
            _selectedVisitForPayment = visit;
            InstallmentPatientNameText.Text = $"للمريض: {_currentModalPatient?.FullName ?? ""} (خدمة: {visit.ServiceType})";
            InstallmentTotalFeesText.Text = $"{visit.TotalFees:N0} ج.م";
            InstallmentPaidAmountText.Text = $"{visit.DepositAmount:N0} ج.م";
            InstallmentRemainingText.Text = $"{visit.RemainingAmount:N0} ج.م";

            InstallmentPaymentInput.Text = visit.RemainingAmount > 0 ? visit.RemainingAmount.ToString("0") : "100";
            PayInstallmentModal.Visibility = Visibility.Visible;
            InstallmentPaymentInput.Focus();
            InstallmentPaymentInput.SelectAll();
        }
    }

    private void ClosePayInstallmentModal_Click(object sender, RoutedEventArgs e)
    {
        PayInstallmentModal.Visibility = Visibility.Collapsed;
        _selectedVisitForPayment = null;
    }

    private async void ConfirmPayInstallment_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVisitForPayment == null) return;

        if (!decimal.TryParse(InstallmentPaymentInput.Text.Trim(), out var amount) || amount <= 0)
        {
            ClinicMessageBox.Show("يرجى إدخال مبلغ صحيح أكبر من الصفر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var method = (InstallmentMethodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "نقداً";

        try
        {
            var success = await _apiClient.PayInstallmentAsync(_selectedVisitForPayment.AppointmentId, amount, method, $"تحصيل قسط من ملف المريض {_currentModalPatient?.FullName}");
            if (success)
            {
                ClinicMessageBox.Show($"تم تحصيل وتسجيل دفعة بمبلغ {amount:N2} ج.م بنجاح!", "نجاح السداد", MessageBoxButton.OK, MessageBoxImage.Information);
                PayInstallmentModal.Visibility = Visibility.Collapsed;
                _selectedVisitForPayment = null;

                // تحديث ملف المريض الحالي
                if (_currentModalPatient != null)
                {
                    var reloaded = await _apiClient.GetPatientByIdAsync(_currentModalPatient.Id);
                    if (reloaded != null)
                    {
                        _currentModalPatient = reloaded;
                        VisitsHistoryGrid.ItemsSource = reloaded.Visits;
                    }
                }
            }
            else
            {
                ClinicMessageBox.Show("تعذر تسجيل الدفعة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء تسجيل الدفعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================
    // 🦷 فتح خريطة الأسنان التفاعلية (Odontogram)
    // =========================================================

    private void OpenDentalChart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientDto patient)
        {
            var chartWindow = new DentalChartingWindow(_apiClient, patient.Id, patient.FullName);
            chartWindow.Owner = Window.GetWindow(this);
            chartWindow.ShowDialog();
        }
    }

    // =========================================================
    // 💊 فتح نافذة الروشتة الإلكترونية الذكية (e-Prescription)
    // =========================================================

    private void OpenPrescription_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientDto patient)
        {
            var rxDialog = new PrescriptionDialog(_apiClient, patient.Id, patient.FullName, patient.PhoneNumber);
            rxDialog.Owner = Window.GetWindow(this);
            rxDialog.ShowDialog();
        }
    }

    // =========================================================
    // 🟢 فتح محادثة الواتساب المباشرة
    // =========================================================

    private void OpenWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientDto patient)
        {
            var formattedPhone = SeddikClinic.Core.Helpers.WhatsAppNotificationHelper.FormatPhoneNumberForWhatsApp(patient.PhoneNumber);
            var url = $"https://wa.me/{formattedPhone}?text={Uri.EscapeDataString($"مرحباً بك أستاذ/ة {patient.FullName} 🌸\nتحياتنا من عيادة د. صديق لطب وجراحة الأسنان 🦷✨\nيسعدنا تواصلك معنا دائماً!")}";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ClinicMessageBox.Show($"تعذر فتح الواتساب: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // =========================================================
    // 🩺 إضافة وتعديل الخدمات الطبية والرسوم للمريض
    // =========================================================

    private PatientDto? _selectedPatientForService;

    private readonly List<(string Name, decimal Price)> _standardServices = new()
    {
        ("كشف واستشارة طبية شاملة", 250m),
        ("تنظيف وتلميع الأسنان وإزالة الجير (Scaling & Polishing)", 400m),
        ("حشو تجميلي كومبوزيت (Composite Filling)", 500m),
        ("جلسة علاج جذور وعصب (Root Canal Treatment)", 850m),
        ("تركيب طربوش زيركون (Zirconia Crown)", 1800m),
        ("تركيب طربوش بورسلين (Porcelain Crown)", 1200m),
        ("خلع ضرس عادي (Simple Extraction)", 350m),
        ("خلع جراحي لضرس العقل (Surgical Extraction)", 1200m),
        ("جلسة تبييض أسنان ليزر / زووم (Teeth Whitening)", 2200m),
        ("تركيب تقويم أسنان - دفعة أولى (Orthodontic Down Payment)", 3500m),
        ("زراعة أسنان ألماني/كوري (Dental Implant)", 6500m),
        ("أشعة ديجيتال بريمال (Periapical X-Ray)", 150m)
    };

    private void AddPatientService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PatientDto patient)
        {
            OpenAddPatientServiceModal(patient);
        }
    }

    private void OpenAddPatientServiceModal(PatientDto patient)
    {
        _selectedPatientForService = patient;
        AddServicePatientNameSubtitle.Text = $"للمريض: {patient.FullName} (كود: {patient.PatientCode})";

        PatientServiceCatalogCombo.ItemsSource = _standardServices.Select(s => s.Name).ToList();
        if (PatientServiceCatalogCombo.Items.Count > 0)
        {
            PatientServiceCatalogCombo.SelectedIndex = 0;
        }

        CustomServiceNameInput.Text = "";
        ServicePriceInput.Text = "250";
        ServiceDiscountInput.Text = "0";
        ServiceDatePicker.SelectedDate = DateTime.Today;
        ServiceNotesInput.Text = "";
        UpdateNetServicePricePreview();

        AddPatientServiceModal.Visibility = Visibility.Visible;
    }

    private void AddServicePriceOrDiscount_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateNetServicePricePreview();
    }

    private void UpdateNetServicePricePreview()
    {
        if (ServicePriceInput == null || ServiceDiscountInput == null || NetServicePricePreviewText == null) return;

        decimal.TryParse(ServicePriceInput.Text.Trim(), out var price);
        decimal.TryParse(ServiceDiscountInput.Text.Trim(), out var discount);
        var net = Math.Max(0, price - discount);

        NetServicePricePreviewText.Text = discount > 0 ? $"{net:N0} ج.م (وفر {discount:N0} ج.م)" : $"{net:N0} ج.م";
    }

    private void CloseAddServiceModal_Click(object sender, RoutedEventArgs e)
    {
        AddPatientServiceModal.Visibility = Visibility.Collapsed;
        _selectedPatientForService = null;
    }

    private void PatientServiceCatalogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PatientServiceCatalogCombo.SelectedItem is string selectedName)
        {
            var matched = _standardServices.FirstOrDefault(s => s.Name == selectedName);
            if (matched.Price > 0)
            {
                ServicePriceInput.Text = matched.Price.ToString("0");
                CustomServiceNameInput.Text = matched.Name;
            }
            UpdateNetServicePricePreview();
        }
    }

    private async void ConfirmAddService_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPatientForService == null) return;

        var serviceName = !string.IsNullOrWhiteSpace(CustomServiceNameInput.Text)
            ? CustomServiceNameInput.Text.Trim()
            : (PatientServiceCatalogCombo.SelectedItem?.ToString() ?? "خدمة طبية");

        if (!decimal.TryParse(ServicePriceInput.Text.Trim(), out var price) || price < 0)
        {
            ClinicMessageBox.Show("يرجى إدخال سعر خدمة صحيح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(ServiceDiscountInput.Text.Trim(), out var discount);
        var netPrice = Math.Max(0, price - discount);
        var serviceDate = ServiceDatePicker.SelectedDate ?? DateTime.Today;

        try
        {
            var appointmentDto = new CreateAppointmentDto
            {
                PatientId = _selectedPatientForService.Id,
                NewPatientFullName = _selectedPatientForService.FullName,
                NewPatientPhone = _selectedPatientForService.PhoneNumber,
                AppointmentDate = serviceDate,
                StartTimeString = DateTime.Now.ToString("HH:mm"),
                DurationMinutes = 30,
                ServiceType = serviceName,
                TotalFees = price,
                DiscountAmount = discount,
                DepositAmount = 0, // لم يدفع بعد - يرحل تلقائياً للفواتير
                Notes = ServiceNotesInput.Text.Trim()
            };

            await _apiClient.CreateAppointmentAsync(appointmentDto);
            ClinicMessageBox.Show(
                discount > 0
                    ? $"تمت إضافة خدمة '{serviceName}' بقيمة ({price:N0} ج.م) مع خصم ({discount:N0} ج.م) وصافي مضاف ({netPrice:N0} ج.م) بنجاح وترحيلها للفواتير!"
                    : $"تمت إضافة خدمة '{serviceName}' بتكلفة {price:N0} ج.م للمريض بنجاح وترحيلها للفواتير والتحصيل!", 
                "نجاح", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);

            AddPatientServiceModal.Visibility = Visibility.Collapsed;
            _selectedPatientForService = null;
            await LoadPatientsAsync();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"فشل إضافة الخدمة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================
    // 🧾 كشف حساب فواتير ومطالبات المريض
    // =========================================================

    private async void OpenPatientInvoices_Click(object sender, RoutedEventArgs e)
    {
        PatientDto? targetPatient = null;
        if (sender is Button btn && btn.DataContext is PatientDto p)
        {
            targetPatient = p;
        }
        else if (sender is MenuItem && PatientsGrid.SelectedItem is PatientDto selected)
        {
            targetPatient = selected;
        }

        if (targetPatient == null) return;

        try
        {
            var full = await _apiClient.GetPatientByIdAsync(targetPatient.Id);
            var visits = full?.Visits ?? new List<PatientVisitHistoryDto>();

            InvoicesModalPatientSubtitle.Text = $"المريض: {targetPatient.FullName} | كود: {targetPatient.PatientCode} | هاتف: {targetPatient.PhoneNumber}";

            var totalFees = visits.Sum(v => v.TotalFees);
            var totalPaid = visits.Sum(v => v.DepositAmount);
            var totalRemaining = Math.Max(0, totalFees - totalPaid);

            InvoicesModalTotalFeesText.Text = $"{totalFees:N2} ج.م";
            InvoicesModalPaidText.Text = $"{totalPaid:N2} ج.م";
            InvoicesModalRemainingText.Text = $"{totalRemaining:N2} ج.م";

            PatientInvoicesGrid.ItemsSource = visits;
            PatientInvoicesModal.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء جلب فواتير المريض: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClosePatientInvoicesModal_Click(object sender, RoutedEventArgs e)
    {
        PatientInvoicesModal.Visibility = Visibility.Collapsed;
    }

    // =========================================================
    // 🖱️ Right-Click ContextMenu Handlers (كليك يمين على المريض)
    // =========================================================

    private PatientDto? GetSelectedPatientFromGrid()
    {
        return PatientsGrid.SelectedItem as PatientDto;
    }

    private void ContextMenu_AddService_Click(object sender, RoutedEventArgs e)
    {
        var patient = GetSelectedPatientFromGrid();
        if (patient != null) OpenAddPatientServiceModal(patient);
    }

    private void ContextMenu_OpenPrescription_Click(object sender, RoutedEventArgs e)
    {
        var patient = GetSelectedPatientFromGrid();
        if (patient != null)
        {
            var rxDialog = new PrescriptionDialog(_apiClient, patient.Id, patient.FullName, patient.PhoneNumber);
            rxDialog.Owner = Window.GetWindow(this);
            rxDialog.ShowDialog();
        }
    }

    private void ContextMenu_OpenDentalChart_Click(object sender, RoutedEventArgs e)
    {
        var patient = GetSelectedPatientFromGrid();
        if (patient != null)
        {
            var chartWindow = new DentalChartingWindow(_apiClient, patient.Id, patient.FullName);
            chartWindow.Owner = Window.GetWindow(this);
            chartWindow.ShowDialog();
        }
    }

    private void ContextMenu_OpenVisitsHistory_Click(object sender, RoutedEventArgs e)
    {
        var patient = GetSelectedPatientFromGrid();
        if (patient != null)
        {
            var fakeBtn = new Button { DataContext = patient };
            ViewMedicalRecord_Click(fakeBtn, e);
        }
    }

    private void ContextMenu_OpenPatientInvoices_Click(object sender, RoutedEventArgs e)
    {
        OpenPatientInvoices_Click(sender, e);
    }

    private void ContextMenu_OpenMedicalHistory_Click(object sender, RoutedEventArgs e)
    {
        var patient = GetSelectedPatientFromGrid();
        if (patient != null) OpenEditMedicalHistoryModal(patient);
    }

    private void ContextMenu_OpenWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        var patient = GetSelectedPatientFromGrid();
        if (patient != null)
        {
            var fakeBtn = new Button { DataContext = patient };
            OpenWhatsApp_Click(fakeBtn, e);
        }
    }

    private void ContextMenu_DeletePatient_Click(object sender, RoutedEventArgs e)
    {
        var patient = GetSelectedPatientFromGrid();
        if (patient != null)
        {
            var fakeBtn = new Button { DataContext = patient };
            DeletePatient_Click(fakeBtn, e);
        }
    }
}
