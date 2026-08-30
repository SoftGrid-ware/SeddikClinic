using System.Windows;
using System.Windows.Controls;
using SeddikClinic.Core.DTOs.Identity;
using SeddikClinic.Core.Enums;
using SeddikClinic.Desktop.Services;

namespace SeddikClinic.Desktop.Views;

public partial class UserManagementView : UserControl
{
    private readonly ClinicApiClient _apiClient;

    public UserManagementView(ClinicApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        Loaded += async (s, e) => await LoadUsersAsync();
    }

    public async Task LoadUsersAsync()
    {
        try
        {
            var users = await _apiClient.GetAllUsersAsync();
            UsersGrid.ItemsSource = users;
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ في جلب المستخدمين: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleAddUserPanel_Click(object sender, RoutedEventArgs e)
    {
        AddUserPanel.Visibility = AddUserPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void RoleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PermFinancials == null || PermExpenses == null || PermCancelExpenses == null || 
            PermAppointments == null || PermPatients == null || PermExport == null || PermUsers == null)
        {
            return;
        }

        if (RoleCombo.SelectedIndex == 0) // مدير
        {
            PermFinancials.IsChecked = true;
            PermExpenses.IsChecked = true;
            PermCancelExpenses.IsChecked = true;
            PermAppointments.IsChecked = true;
            PermPatients.IsChecked = true;
            PermQuickActions.IsChecked = true;
            PermEditPrescriptions.IsChecked = true;
            PermExport.IsChecked = true;
            PermUsers.IsChecked = true;
        }
        else // مساعد
        {
            PermFinancials.IsChecked = false;
            PermExpenses.IsChecked = true;
            PermCancelExpenses.IsChecked = false;
            PermAppointments.IsChecked = true;
            PermPatients.IsChecked = true;
            PermQuickActions.IsChecked = true;
            PermEditPrescriptions.IsChecked = false; // افتراضياً للمساعد: عرض وطباعة فقط
            PermExport.IsChecked = false;
            PermUsers.IsChecked = false;
        }
    }

    private async void SaveUser_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FullNameInput.Text) || string.IsNullOrWhiteSpace(UsernameInput.Text) || string.IsNullOrWhiteSpace(PasswordInput.Text))
        {
            ClinicMessageBox.Show("يرجى إدخال جميع البيانات الأساسية للمستخدم.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var role = RoleCombo.SelectedIndex switch
        {
            0 => UserRole.Manager,
            2 => UserRole.Doctor,
            _ => UserRole.Assistant
        };

        var dto = new CreateUserDto
        {
            FullName = FullNameInput.Text.Trim(),
            Username = UsernameInput.Text.Trim(),
            Password = PasswordInput.Text.Trim(),
            Role = role,
            CanViewFinancials = PermFinancials.IsChecked == true,
            CanManageExpenses = PermExpenses.IsChecked == true,
            CanCancelExpenses = PermCancelExpenses.IsChecked == true,
            CanManageAppointments = PermAppointments.IsChecked == true,
            CanManagePatients = PermPatients.IsChecked == true,
            CanUseQuickActions = PermQuickActions.IsChecked == true,
            CanEditPrescriptions = PermEditPrescriptions.IsChecked == true,
            CanExportReports = PermExport.IsChecked == true,
            CanManageUsers = PermUsers.IsChecked == true
        };

        try
        {
            await _apiClient.CreateUserAsync(dto);
            ClinicMessageBox.Show($"تم إنشاء حساب المستخدم '{dto.FullName}' وتعيين صلاحياته بنجاح!", "تم بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);

            FullNameInput.Text = "";
            UsernameInput.Text = "";
            PasswordInput.Text = "";
            AddUserPanel.Visibility = Visibility.Collapsed;

            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"فشل إنشاء المستخدم: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private UserDto? _selectedUserForPermissions;

    private void EditPermissions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is UserDto user)
        {
            _selectedUserForPermissions = user;

            EditPermFullNameText.Text = user.FullName;
            EditPermUsernameText.Text = $"@{user.Username}";
            EditPermRoleBadgeText.Text = user.RoleBadge;
            EditPermUserSubtitle.Text = $"تحديد الصلاحيات الدقيقة لحساب: {user.FullName}";

            ModalPermFinancials.IsChecked = user.CanViewFinancials;
            ModalPermAppointments.IsChecked = user.CanManageAppointments;
            ModalPermPatients.IsChecked = user.CanManagePatients;
            ModalPermQuickActions.IsChecked = user.CanUseQuickActions;
            ModalPermEditPrescriptions.IsChecked = user.CanEditPrescriptions;
            ModalPermExpenses.IsChecked = user.CanManageExpenses;
            ModalPermCancelExpenses.IsChecked = user.CanCancelExpenses;
            ModalPermExport.IsChecked = user.CanExportReports;
            ModalPermUsers.IsChecked = user.CanManageUsers;

            EditPermissionsModal.Visibility = Visibility.Visible;
        }
    }

    private void CloseEditPermissionsModal_Click(object sender, RoutedEventArgs e)
    {
        EditPermissionsModal.Visibility = Visibility.Collapsed;
        _selectedUserForPermissions = null;
    }

    private async void SavePermissions_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUserForPermissions == null) return;

        try
        {
            var newPermissions = new UpdateUserPermissionsDto
            {
                CanViewFinancials = ModalPermFinancials.IsChecked == true,
                CanManageAppointments = ModalPermAppointments.IsChecked == true,
                CanManagePatients = ModalPermPatients.IsChecked == true,
                CanUseQuickActions = ModalPermQuickActions.IsChecked == true,
                CanEditPrescriptions = ModalPermEditPrescriptions.IsChecked == true,
                CanManageExpenses = ModalPermExpenses.IsChecked == true,
                CanCancelExpenses = ModalPermCancelExpenses.IsChecked == true,
                CanExportReports = ModalPermExport.IsChecked == true,
                CanManageUsers = ModalPermUsers.IsChecked == true
            };

            var success = await _apiClient.UpdateUserPermissionsAsync(_selectedUserForPermissions.Id, newPermissions);
            if (success)
            {
                EditPermissionsModal.Visibility = Visibility.Collapsed;
                await LoadUsersAsync();
                ClinicMessageBox.Show($"تم تحديث وحفظ صلاحيات المستخدم '{_selectedUserForPermissions.FullName}' بنجاح!", "تم التحديث", MessageBoxButton.OK, MessageBoxImage.Information);
                _selectedUserForPermissions = null;
            }
            else
            {
                ClinicMessageBox.Show("تعذر تحديث الصلاحيات.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ClinicMessageBox.Show($"خطأ أثناء حفظ الصلاحيات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ToggleUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is UserDto user)
        {
            var success = await _apiClient.ToggleUserStatusAsync(user.Id);
            if (success)
            {
                var actionText = user.IsActive ? "تجميد وإيقاف" : "تفعيل وتنشيط";
                ClinicMessageBox.Show($"تم {actionText} حساب المستخدم '{user.FullName}' بنجاح!", "حالة الحساب", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadUsersAsync();
            }
        }
    }

    private async void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is UserDto user)
        {
            var res = ClinicMessageBox.Show($"هل أنت متأكد من رغبتك في حذف حساب '{user.FullName}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                await _apiClient.DeleteUserAsync(user.Id);
                await LoadUsersAsync();
            }
        }
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadUsersAsync();
    }
}
