using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.Entities.Appointments;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public class ToothConditionItem : INotifyPropertyChanged
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#10B981";
    public bool IsCustom { get; set; } = false;

    public Brush ColorBrush
    {
        get
        {
            try
            {
                return (Brush?)new BrushConverter().ConvertFromString(ColorHex) ?? new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ToothViewModel : INotifyPropertyChanged
{
    private ToothCondition _condition = ToothCondition.Healthy;
    private string? _customConditionName;
    private string? _affectedSurfaces;
    private decimal _estimatedCost;
    private bool _isCompleted;
    private string? _notes;
    private bool _isSelected;

    public int ToothNumber { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string? CustomConditionName
    {
        get => _customConditionName;
        set
        {
            if (_customConditionName != value)
            {
                _customConditionName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShortConditionCode));
                OnPropertyChanged(nameof(ConditionBrush));
                OnPropertyChanged(nameof(ConditionForegroundBrush));
            }
        }
    }

    public ToothCondition Condition
    {
        get => _condition;
        set
        {
            if (_condition != value)
            {
                _condition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShortConditionCode));
                OnPropertyChanged(nameof(ConditionBrush));
                OnPropertyChanged(nameof(ConditionForegroundBrush));
                OnPropertyChanged(nameof(BorderBrush));
            }
        }
    }

    public string? AffectedSurfaces
    {
        get => _affectedSurfaces;
        set { _affectedSurfaces = value; OnPropertyChanged(); }
    }

    public decimal EstimatedCost
    {
        get => _estimatedCost;
        set { _estimatedCost = value; OnPropertyChanged(); }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set { _isCompleted = value; OnPropertyChanged(); OnPropertyChanged(nameof(BorderBrush)); }
    }

    public string? Notes
    {
        get => _notes;
        set { _notes = value; OnPropertyChanged(); }
    }

    public string ShortConditionCode
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomConditionName))
                return CustomConditionName.Length > 8 ? CustomConditionName.Substring(0, 8) : CustomConditionName;

            return Condition switch
            {
                ToothCondition.Healthy => "سليم",
                ToothCondition.Decayed => "تسوس",
                ToothCondition.Filled => "حشو",
                ToothCondition.RootCanal => "عصب",
                ToothCondition.Crown => "زيركون",
                ToothCondition.Extracted => "مخلوع",
                ToothCondition.Implant => "زراعة",
                ToothCondition.Bridge => "جسر",
                ToothCondition.Veneer => "فينير",
                ToothCondition.Impacted => "مدفون",
                ToothCondition.Orthodontic => "تقويم",
                _ => "سليم"
            };
        }
    }

    public Brush ConditionBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomConditionName))
            {
                var custom = DentalChartingWindow.CurrentConditions.FirstOrDefault(c => c.NameAr == CustomConditionName || c.Code == CustomConditionName);
                if (custom != null) return custom.ColorBrush;
            }

            return Condition switch
            {
                ToothCondition.Healthy => new SolidColorBrush(Color.FromRgb(16, 185, 129)),   // #10B981
                ToothCondition.Decayed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),    // #EF4444
                ToothCondition.Filled => new SolidColorBrush(Color.FromRgb(59, 130, 246)),    // #3B82F6
                ToothCondition.RootCanal => new SolidColorBrush(Color.FromRgb(139, 92, 246)), // #8B5CF6
                ToothCondition.Crown => new SolidColorBrush(Color.FromRgb(245, 158, 11)),     // #F59E0B
                ToothCondition.Extracted => new SolidColorBrush(Color.FromRgb(100, 116, 139)),// #64748B
                ToothCondition.Implant => new SolidColorBrush(Color.FromRgb(6, 182, 212)),    // #06B6D4
                ToothCondition.Bridge => new SolidColorBrush(Color.FromRgb(236, 72, 153)),    // #EC4899
                ToothCondition.Veneer => new SolidColorBrush(Color.FromRgb(20, 184, 166)),    // #14B8A6
                ToothCondition.Impacted => new SolidColorBrush(Color.FromRgb(71, 85, 105)),   // #475569
                ToothCondition.Orthodontic => new SolidColorBrush(Color.FromRgb(99, 102, 241)),// #6366F1
                _ => new SolidColorBrush(Color.FromRgb(226, 232, 240))
            };
        }
    }

    public Brush ConditionForegroundBrush => Brushes.White;

    public Brush BorderBrush => IsCompleted ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class DentalChartingWindow : Window
{
    private readonly ClinicApiClient _apiClient;
    private readonly Guid _patientId;
    private readonly string _patientName;
    private readonly ObservableCollection<ToothViewModel> _upperTeeth = new();
    private readonly ObservableCollection<ToothViewModel> _lowerTeeth = new();
    private ToothViewModel? _selectedTooth;
    private bool _isPediatric = false;

    public static ObservableCollection<ToothConditionItem> CurrentConditions { get; } = new();

    private static readonly string ConditionsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SeddikClinic", "dental_conditions.json");

    // Adult FDI Notation
    private static readonly int[] AdultUpperTeeth = { 18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28 };
    private static readonly int[] AdultLowerTeeth = { 48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38 };

    // Pediatric FDI Notation
    private static readonly int[] PediatricUpperTeeth = { 55, 54, 53, 52, 51, 61, 62, 63, 64, 65 };
    private static readonly int[] PediatricLowerTeeth = { 85, 84, 83, 82, 81, 71, 72, 73, 74, 75 };

    public DentalChartingWindow(ClinicApiClient apiClient, Guid patientId, string patientName)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _patientId = patientId;
        _patientName = patientName;

        PatientNameBadge.Text = $"المريض: {_patientName}";
        UpperTeethItemsControl.ItemsSource = _upperTeeth;
        LowerTeethItemsControl.ItemsSource = _lowerTeeth;

        InitConditionsCatalog();

        ToothConditionCombo.ItemsSource = CurrentConditions;
        LegendItemsControl.ItemsSource = CurrentConditions;
        ConditionManagerItemsControl.ItemsSource = CurrentConditions;

        if (CurrentConditions.Any())
        {
            ToothConditionCombo.SelectedIndex = 0;
        }

        Loaded += async (s, e) => await InitializeChartAsync();
    }

    private void InitConditionsCatalog()
    {
        if (CurrentConditions.Count > 0) return;

        LoadSavedConditions();

        if (CurrentConditions.Count == 0)
        {
            // Default built-in standard conditions
            var defaults = new List<ToothConditionItem>
            {
                new() { Code = "Healthy", NameAr = "سليم (Healthy)", ColorHex = "#10B981", IsCustom = false },
                new() { Code = "Decayed", NameAr = "تسوس / نخر (Decayed)", ColorHex = "#EF4444", IsCustom = false },
                new() { Code = "Filled", NameAr = "حشو كمبوزيت (Filled)", ColorHex = "#3B82F6", IsCustom = false },
                new() { Code = "RootCanal", NameAr = "علاج عصب وجذور (Root Canal)", ColorHex = "#8B5CF6", IsCustom = false },
                new() { Code = "Crown", NameAr = "طربوش / زيركون (Crown)", ColorHex = "#F59E0B", IsCustom = false },
                new() { Code = "Extracted", NameAr = "مخلوع / مفقود (Extracted)", ColorHex = "#64748B", IsCustom = false },
                new() { Code = "Implant", NameAr = "زراعة سن (Implant)", ColorHex = "#06B6D4", IsCustom = false },
                new() { Code = "Bridge", NameAr = "جسر / كوبري (Bridge)", ColorHex = "#EC4899", IsCustom = false },
                new() { Code = "Veneer", NameAr = "فينير / ابتسامة (Veneer)", ColorHex = "#14B8A6", IsCustom = false },
                new() { Code = "Impacted", NameAr = "سن مدفون / منطمر (Impacted)", ColorHex = "#475569", IsCustom = false },
                new() { Code = "Orthodontic", NameAr = "تقويم أسنان (Orthodontic)", ColorHex = "#6366F1", IsCustom = false }
            };

            foreach (var item in defaults)
            {
                CurrentConditions.Add(item);
            }
        }
    }

    private void LoadSavedConditions()
    {
        try
        {
            if (File.Exists(ConditionsFilePath))
            {
                var json = File.ReadAllText(ConditionsFilePath);
                var list = JsonSerializer.Deserialize<List<ToothConditionItem>>(json);
                if (list != null && list.Count > 0)
                {
                    CurrentConditions.Clear();
                    foreach (var item in list)
                    {
                        CurrentConditions.Add(item);
                    }
                }
            }
        }
        catch { }
    }

    private void SaveConditionsToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConditionsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(CurrentConditions.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConditionsFilePath, json);
        }
        catch { }
    }

    private async Task InitializeChartAsync()
    {
        BuildTeethGrid();
        await LoadPatientChartDataAsync();
    }

    private void BuildTeethGrid()
    {
        _upperTeeth.Clear();
        _lowerTeeth.Clear();

        var upperList = _isPediatric ? PediatricUpperTeeth : AdultUpperTeeth;
        var lowerList = _isPediatric ? PediatricLowerTeeth : AdultLowerTeeth;

        foreach (var num in upperList)
        {
            _upperTeeth.Add(new ToothViewModel { ToothNumber = num });
        }

        foreach (var num in lowerList)
        {
            _lowerTeeth.Add(new ToothViewModel { ToothNumber = num });
        }
    }

    private async Task LoadPatientChartDataAsync()
    {
        try
        {
            var chart = await _apiClient.GetPatientDentalChartAsync(_patientId);
            if (chart != null)
            {
                TotalEstimatedPlanCostText.Text = $"{chart.TotalEstimatedTreatmentCost:N0} ج.م";

                foreach (var toothDto in chart.Teeth)
                {
                    var vm = _upperTeeth.FirstOrDefault(t => t.ToothNumber == toothDto.ToothNumber)
                             ?? _lowerTeeth.FirstOrDefault(t => t.ToothNumber == toothDto.ToothNumber);

                    if (vm != null)
                    {
                        vm.Condition = toothDto.Condition;
                        vm.AffectedSurfaces = toothDto.AffectedSurfaces;
                        vm.Notes = toothDto.Notes;
                        vm.EstimatedCost = toothDto.EstimatedCost;
                        vm.IsCompleted = toothDto.IsCompleted;

                        // Check if custom condition is embedded in notes e.g. [CustomCondition:تبييض]
                        if (!string.IsNullOrEmpty(toothDto.Notes) && toothDto.Notes.Contains("[CustomCondition:"))
                        {
                            var startIdx = toothDto.Notes.IndexOf("[CustomCondition:") + 17;
                            var endIdx = toothDto.Notes.IndexOf("]", startIdx);
                            if (endIdx > startIdx)
                            {
                                vm.CustomConditionName = toothDto.Notes.Substring(startIdx, endIdx - startIdx);
                            }
                        }
                    }
                }

                DentalImagesListView.ItemsSource = chart.Images;
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب بيانات خريطة الأسنان: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToothButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ToothViewModel tooth)
        {
            foreach (var t in _upperTeeth) t.IsSelected = false;
            foreach (var t in _lowerTeeth) t.IsSelected = false;

            _selectedTooth = tooth;
            _selectedTooth.IsSelected = true;

            SelectedToothTitle.Text = $"السن رقم #{tooth.ToothNumber}";
            SelectedToothSubtitle.Text = $"الحالة الحالية: {tooth.ShortConditionCode}";

            // Set condition in combo
            if (!string.IsNullOrEmpty(tooth.CustomConditionName))
            {
                var match = CurrentConditions.FirstOrDefault(c => c.NameAr == tooth.CustomConditionName || c.Code == tooth.CustomConditionName);
                if (match != null) ToothConditionCombo.SelectedItem = match;
            }
            else
            {
                var match = CurrentConditions.FirstOrDefault(c => c.Code == tooth.Condition.ToString());
                if (match != null) ToothConditionCombo.SelectedItem = match;
            }

            // Surfaces
            var surfaces = tooth.AffectedSurfaces ?? "";
            SurfaceM.IsChecked = surfaces.Contains("M");
            SurfaceO.IsChecked = surfaces.Contains("O");
            SurfaceD.IsChecked = surfaces.Contains("D");
            SurfaceB.IsChecked = surfaces.Contains("B");
            SurfaceL.IsChecked = surfaces.Contains("L");

            ToothEstimatedCostInput.Text = tooth.EstimatedCost.ToString("0");
            
            // Clean notes display
            var notesDisplay = tooth.Notes ?? "";
            if (notesDisplay.Contains("[CustomCondition:"))
            {
                var idx = notesDisplay.IndexOf("[CustomCondition:");
                var end = notesDisplay.IndexOf("]", idx);
                if (end >= 0)
                {
                    notesDisplay = notesDisplay.Remove(idx, end - idx + 1).Trim();
                }
            }
            ToothNotesInput.Text = notesDisplay;
            ToothCompletedCheckBox.IsChecked = tooth.IsCompleted;
        }
    }

    private async void SaveToothRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTooth == null)
        {
            ClinicMessageBox.Show("يرجى اختيار سن من الخريطة أولاً لتعديله.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedConditionItem = ToothConditionCombo.SelectedItem as ToothConditionItem;
        var selectedCode = selectedConditionItem?.Code ?? "Healthy";
        
        ToothCondition cond = ToothCondition.Healthy;
        bool isCustom = selectedConditionItem?.IsCustom == true || !Enum.TryParse<ToothCondition>(selectedCode, out cond);

        var surfaces = "";
        if (SurfaceM.IsChecked == true) surfaces += "M";
        if (SurfaceO.IsChecked == true) surfaces += "O";
        if (SurfaceD.IsChecked == true) surfaces += "D";
        if (SurfaceB.IsChecked == true) surfaces += "B";
        if (SurfaceL.IsChecked == true) surfaces += "L";

        decimal.TryParse(ToothEstimatedCostInput.Text.Trim(), out var cost);

        var cleanNotes = ToothNotesInput.Text.Trim();
        var fullNotes = cleanNotes;
        if (isCustom && selectedConditionItem != null)
        {
            fullNotes = $"[CustomCondition:{selectedConditionItem.NameAr}] " + cleanNotes;
        }

        var dto = new UpdateToothRecordDto
        {
            PatientId = _patientId,
            ToothNumber = _selectedTooth.ToothNumber,
            Condition = isCustom ? ToothCondition.Healthy : cond,
            AffectedSurfaces = surfaces,
            Notes = fullNotes,
            EstimatedCost = cost,
            IsCompleted = ToothCompletedCheckBox.IsChecked == true
        };

        try
        {
            var result = await _apiClient.UpdateToothRecordAsync(dto);
            if (result != null)
            {
                _selectedTooth.Condition = dto.Condition;
                _selectedTooth.CustomConditionName = isCustom ? selectedConditionItem?.NameAr : null;
                _selectedTooth.AffectedSurfaces = surfaces;
                _selectedTooth.Notes = fullNotes;
                _selectedTooth.EstimatedCost = cost;
                _selectedTooth.IsCompleted = dto.IsCompleted;

                SelectedToothSubtitle.Text = $"الحالة الحالية: {_selectedTooth.ShortConditionCode}";

                // Refresh overall estimated cost
                var total = _upperTeeth.Sum(t => t.EstimatedCost) + _lowerTeeth.Sum(t => t.EstimatedCost);
                TotalEstimatedPlanCostText.Text = $"{total:N0} ج.م";

                ClinicMessageBox.Show($"تم حفظ حالة السن #{_selectedTooth.ToothNumber} بنجاح!", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في حفظ السن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AdultTeeth_Click(object sender, RoutedEventArgs e)
    {
        _isPediatric = false;
        AdultTeethBtn.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
        AdultTeethBtn.Foreground = Brushes.White;
        PediatricTeethBtn.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        PediatricTeethBtn.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        BuildTeethGrid();
        _ = LoadPatientChartDataAsync();
    }

    private void PediatricTeeth_Click(object sender, RoutedEventArgs e)
    {
        _isPediatric = true;
        PediatricTeethBtn.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
        PediatricTeethBtn.Foreground = Brushes.White;
        AdultTeethBtn.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        AdultTeethBtn.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        BuildTeethGrid();
        _ = LoadPatientChartDataAsync();
    }

    private async void ResetChart_Click(object sender, RoutedEventArgs e)
    {
        var confirm = ClinicMessageBox.Show("هل أنت متأكد من رغبتك في إعادة ضبط خريطة أسنان المريض كاملة وحذف الحالات السابقة؟", "تأكيد إعادة الضبط", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            await _apiClient.ResetPatientTeethAsync(_patientId);
            BuildTeethGrid();
            TotalEstimatedPlanCostText.Text = "0 ج.م";
            ClinicMessageBox.Show("تمت إعادة ضبط خريطة الأسنان بنجاح.", "تمت العملية", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // =========================================================
    // ⚙️ إدارة وتخصيص حالات الأسنان (Add / Delete Conditions)
    // =========================================================

    private void OpenConditionManager_Click(object sender, RoutedEventArgs e)
    {
        NewConditionNameInput.Text = "";
        ConditionManagerModal.Visibility = Visibility.Visible;
    }

    private void CloseConditionManager_Click(object sender, RoutedEventArgs e)
    {
        ConditionManagerModal.Visibility = Visibility.Collapsed;
    }

    private void AddNewCondition_Click(object sender, RoutedEventArgs e)
    {
        var name = NewConditionNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ClinicMessageBox.Show("يرجى إدخال اسم الحالة السنية.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var colorTag = (NewConditionColorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "#10B981";

        // Check if already exists
        if (CurrentConditions.Any(c => c.NameAr.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            ClinicMessageBox.Show("هذه الحالة موجودة بالفعل في القائمة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newItem = new ToothConditionItem
        {
            Code = $"Custom_{Guid.NewGuid().ToString().Substring(0, 8)}",
            NameAr = name,
            ColorHex = colorTag,
            IsCustom = true
        };

        CurrentConditions.Add(newItem);
        SaveConditionsToFile();

        NewConditionNameInput.Text = "";
        ToothConditionCombo.SelectedItem = newItem;
        ClinicMessageBox.Show($"تمت إضافة حالة '{name}' بنجاح لقائمة الحالات!", "تمت الإضافة", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteConditionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ToothConditionItem item)
        {
            if (CurrentConditions.Count <= 1)
            {
                ClinicMessageBox.Show("يجب الإبقاء على حالة سنية واحدة على الأقل في النظام.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = ClinicMessageBox.Show($"هل أنت متأكد من حذف حالة '{item.NameAr}' من القائمة؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                CurrentConditions.Remove(item);
                SaveConditionsToFile();

                if (ToothConditionCombo.SelectedItem == item)
                {
                    ToothConditionCombo.SelectedIndex = 0;
                }

                ClinicMessageBox.Show("تم حذف الحالة بنجاح.", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private async void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dental Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            Title = "اختر صورة الأشعة أو حالة الأسنان"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var title = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                var dto = new CreateDentalImageDto
                {
                    PatientId = _patientId,
                    Title = title,
                    ImageType = DentalImageType.PanoramicXRay,
                    ImageUrl = openFileDialog.FileName
                };

                var created = await _apiClient.AddPatientImageAsync(dto);
                if (created != null)
                {
                    ClinicMessageBox.Show("تمت إضافة صورة الأشعة لملف المريض بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadPatientChartDataAsync();
                }
            }
            catch (Exception ex)
            {
                ClinicMessageBox.Show($"خطأ في رفع الصورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

