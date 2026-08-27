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
}
