using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Helpers;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class PrescriptionDialog : Window
{
    private readonly ClinicApiClient _apiClient;
    private readonly Guid _patientId;
    private readonly string _patientName;
    private readonly string _patientPhone;
    private readonly Guid? _appointmentId;
    private readonly ObservableCollection<CreatePrescriptionItemDto> _items = new();
    private List<DentalDrugCatalogItemDto> _drugCatalog = new();
    private PrescriptionDto? _lastSavedPrescription;
    private readonly bool _isReadOnly;

    public PrescriptionDialog(ClinicApiClient apiClient, Guid patientId, string patientName, string patientPhone, Guid? appointmentId = null, bool isReadOnly = false)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _patientId = patientId;
        _patientName = patientName;
        _patientPhone = patientPhone;
        _appointmentId = appointmentId;
        _isReadOnly = isReadOnly;

        PatientInfoBadge.Text = $"المريض: {_patientName} ({_patientPhone})";
        PrescriptionItemsGrid.ItemsSource = _items;

        ApplyReadOnlyMode();

        Loaded += async (s, e) =>
        {
            if (!_isReadOnly)
            {
                await LoadCatalogAsync();
            }
            await LoadExistingPrescriptionAsync();
        };
    }

    private void ApplyReadOnlyMode()
    {
        if (!_isReadOnly) return;

        Title = "عرض ومعاينة الروشتة الطبية (للطباعة والمعاينة فقط) - عيادة د. صديق";
        DialogTitleText.Text = "عرض وطباعة الروشتة الطبية";
        DialogSubtitleText.Text = "صلاحية استعراض الروشتة للمريض وطباعتها أو إرسالها عبر الواتساب (وضع القراءة فقط)";

        ReadOnlyBadge.Visibility = Visibility.Visible;
        AddDrugPanel.Visibility = Visibility.Collapsed;
        DrugPickerColumn.Width = new GridLength(0);

        if (ItemActionColumn != null)
        {
            ItemActionColumn.Visibility = Visibility.Collapsed;
        }

        PrescriptionDiagnosisInput.IsReadOnly = true;
        PrescriptionDiagnosisInput.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));

        GeneralInstructionsInput.IsReadOnly = true;
        GeneralInstructionsInput.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));

        SavePrescriptionBtn.Visibility = Visibility.Collapsed;
    }

    private async Task LoadExistingPrescriptionAsync()
    {
        try
        {
            var list = await _apiClient.GetPatientPrescriptionsAsync(_patientId);
            var existing = (_appointmentId.HasValue && _appointmentId.Value != Guid.Empty)
                ? list.FirstOrDefault(p => p.AppointmentId == _appointmentId.Value) ?? list.FirstOrDefault()
                : list.FirstOrDefault();

            if (existing != null)
            {
                _lastSavedPrescription = existing;
                PrescriptionDiagnosisInput.Text = existing.Diagnosis ?? "";
                GeneralInstructionsInput.Text = existing.GeneralInstructions ?? "";

                _items.Clear();
                foreach (var item in existing.Items)
                {
                    _items.Add(new CreatePrescriptionItemDto
                    {
                        MedicationName = item.MedicationName,
                        Dosage = item.Dosage,
                        Frequency = item.Frequency,
                        Duration = item.Duration,
                        Instructions = item.Instructions
                    });
                }
            }
        }
        catch
        {
            // تجاهل أي خطأ في التحميل المسبق
        }
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            _drugCatalog = await _apiClient.GetCommonDentalDrugsCatalogAsync();
            CommonDrugsCombo.ItemsSource = _drugCatalog;
            if (_drugCatalog.Any())
            {
                CommonDrugsCombo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب كتالوج الأدوية: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CommonDrugsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CommonDrugsCombo.SelectedItem is DentalDrugCatalogItemDto drug)
        {
            DrugNameInput.Text = drug.TradeName;
            DrugDosageInput.Text = drug.DefaultDosage;
            DrugFrequencyInput.Text = drug.DefaultFrequency;
            DrugDurationInput.Text = drug.DefaultDuration;
            DrugInstructionsInput.Text = drug.DefaultInstructions ?? "";
        }
    }

    private void AddDrugItem_Click(object sender, RoutedEventArgs e)
    {
        var name = DrugNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ClinicMessageBox.Show("يرجى إدخال اسم الدواء أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _items.Add(new CreatePrescriptionItemDto
        {
            MedicationName = name,
            Dosage = DrugDosageInput.Text.Trim(),
            Frequency = DrugFrequencyInput.Text.Trim(),
            Duration = DrugDurationInput.Text.Trim(),
            Instructions = DrugInstructionsInput.Text.Trim()
        });

        // Clear drug input for next item
        DrugNameInput.Text = "";
    }

    private void RemoveDrugItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CreatePrescriptionItemDto item)
        {
            _items.Remove(item);
        }
    }

    private async Task<bool> ExecuteSaveAsync()
    {
        if (!_items.Any())
        {
            ClinicMessageBox.Show("يرجى إضافة دواء واحد على الأقل للروشتة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var dto = new CreatePrescriptionDto
        {
            PatientId = _patientId,
            AppointmentId = _appointmentId,
            DoctorName = "د. صديق",
            Diagnosis = PrescriptionDiagnosisInput.Text.Trim(),
            GeneralInstructions = GeneralInstructionsInput.Text.Trim(),
            Items = _items.ToList()
        };

        try
        {
            _lastSavedPrescription = await _apiClient.CreatePrescriptionAsync(dto);
            return _lastSavedPrescription != null;
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"فشل حفظ الروشتة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async void SavePrescription_Click(object sender, RoutedEventArgs e)
    {
        if (await ExecuteSaveAsync())
        {
            ClinicMessageBox.Show($"تم إصدار وحفظ الروشتة رقم #{_lastSavedPrescription?.PrescriptionNumber} بنجاح ومزامنتها مع تطبيق المريض!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void SendWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSavedPrescription == null)
        {
            var saved = await ExecuteSaveAsync();
            if (!saved) return;
        }

        if (_lastSavedPrescription != null)
        {
            var url = WhatsAppNotificationHelper.GeneratePrescriptionShareUrl(_lastSavedPrescription);
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ClinicMessageBox.Show($"تعذر فتح الواتساب: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async void PrintPrescription_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSavedPrescription == null)
        {
            var saved = await ExecuteSaveAsync();
            if (!saved) return;
        }

        var printDlg = new PrintDialog();
        if (printDlg.ShowDialog() == true)
        {
            // Simple document printing
            var doc = new System.Windows.Documents.FlowDocument
            {
                PagePadding = new Thickness(40),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FlowDirection = FlowDirection.RightToLeft
            };

            var header = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"عيادة د. صديق لطب وجراحة الأسنان 🦷\nالروشتة الطبية رقم: #{_lastSavedPrescription?.PrescriptionNumber}"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            };
            doc.Blocks.Add(header);

            var patientPara = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"اسم المريض: {_patientName} • التاريخ: {_lastSavedPrescription?.FormattedDate}\nالتشخيص: {_lastSavedPrescription?.Diagnosis}"))
            {
                FontSize = 13
            };
            doc.Blocks.Add(patientPara);

            var list = new System.Windows.Documents.List();
            if (_lastSavedPrescription?.Items != null)
            {
                foreach (var it in _lastSavedPrescription.Items)
                {
                    list.ListItems.Add(new System.Windows.Documents.ListItem(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{it.MedicationName} ({it.Dosage}) - {it.Frequency} ({it.Duration})"))));
                }
            }
            doc.Blocks.Add(list);

            if (!string.IsNullOrWhiteSpace(_lastSavedPrescription?.GeneralInstructions))
            {
                doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"تعليمات: {_lastSavedPrescription.GeneralInstructions}")) { FontSize = 11 });
            }

            var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
            printDlg.PrintDocument(paginator, "Medical Prescription");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
