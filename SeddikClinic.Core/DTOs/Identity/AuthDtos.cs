using SeddikClinic.Core.Enums;

namespace SeddikClinic.Core.DTOs.Identity;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }
    public UserDto? User { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; }
    public string RoleNameAr => Role switch
    {
        UserRole.Manager => "المدير / الطبيب",
        UserRole.Assistant => "المساعد / الاستقبال",
        UserRole.Doctor => "طبيب أخصائي",
        _ => Role.ToString()
    };
    public string RoleBadge => Role switch
    {
        UserRole.Manager => "👑 مدير المنظومة",
        UserRole.Assistant => "👤 مساعد العيادة",
        UserRole.Doctor => "🩺 طبيب",
        _ => "مستخدم"
    };

    // الصلاحيات
    public bool CanViewFinancials { get; set; }
    public bool CanManageExpenses { get; set; }
    public bool CanCancelExpenses { get; set; }
    public bool CanManageAppointments { get; set; }
    public bool CanManagePatients { get; set; }
    public bool CanExportReports { get; set; }
    public bool CanManageUsers { get; set; }

    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Assistant;

    // الصلاحيات
    public bool CanViewFinancials { get; set; } = false;
    public bool CanManageExpenses { get; set; } = true;
    public bool CanCancelExpenses { get; set; } = false;
    public bool CanManageAppointments { get; set; } = true;
    public bool CanManagePatients { get; set; } = true;
    public bool CanExportReports { get; set; } = false;
    public bool CanManageUsers { get; set; } = false;
}

public class UpdateUserPermissionsDto
{
    public bool CanViewFinancials { get; set; }
    public bool CanManageExpenses { get; set; }
    public bool CanCancelExpenses { get; set; }
    public bool CanManageAppointments { get; set; }
    public bool CanManagePatients { get; set; }
    public bool CanExportReports { get; set; }
    public bool CanManageUsers { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
