using Microsoft.EntityFrameworkCore;
using SeddikClinic.Core.DTOs.Identity;
using SeddikClinic.Core.Entities.Identity;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly SeddikClinicDbContext _dbContext;

    public UserService(SeddikClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _dbContext.AppUsers
            .OrderByDescending(u => u.Role == UserRole.Manager)
            .ThenBy(u => u.FullName)
            .ToListAsync();

        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _dbContext.AppUsers.FindAsync(id);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        var username = dto.Username.Trim().ToLower();
        var existing = await _dbContext.AppUsers.AnyAsync(u => u.Username.ToLower() == username);
        if (existing)
        {
            throw new InvalidOperationException($"اسم المستخدم '{dto.Username}' مسجل مسبقاً لمستخدم آخر.");
        }

        var isManager = dto.Role == UserRole.Manager;

        var user = new AppUser
        {
            Username = username,
            PasswordHash = PasswordHasher.HashPassword(dto.Password),
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber?.Trim(),
            Role = dto.Role,
            // إذا كان مدير، يُمنح كامل الصلاحيات تلقائياً
            CanViewFinancials = isManager || dto.CanViewFinancials,
            CanManageExpenses = isManager || dto.CanManageExpenses,
            CanCancelExpenses = isManager || dto.CanCancelExpenses,
            CanManageAppointments = isManager || dto.CanManageAppointments,
            CanManagePatients = isManager || dto.CanManagePatients,
            CanExportReports = isManager || dto.CanExportReports,
            CanManageUsers = isManager || dto.CanManageUsers,
            CanUseQuickActions = isManager || dto.CanUseQuickActions,
            CanEditPrescriptions = isManager || dto.CanEditPrescriptions,
            IsActive = true
        };

        _dbContext.AppUsers.Add(user);
        await _dbContext.SaveChangesAsync();

        return MapToDto(user);
    }

    public async Task<bool> UpdateUserPermissionsAsync(Guid id, UpdateUserPermissionsDto dto)
    {
        var user = await _dbContext.AppUsers.FindAsync(id);
        if (user == null) return false;

        user.CanViewFinancials = dto.CanViewFinancials;
        user.CanManageExpenses = dto.CanManageExpenses;
        user.CanCancelExpenses = dto.CanCancelExpenses;
        user.CanManageAppointments = dto.CanManageAppointments;
        user.CanManagePatients = dto.CanManagePatients;
        user.CanExportReports = dto.CanExportReports;
        user.CanManageUsers = dto.CanManageUsers;
        user.CanUseQuickActions = dto.CanUseQuickActions;
        user.CanEditPrescriptions = dto.CanEditPrescriptions;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleUserStatusAsync(Guid id)
    {
        var user = await _dbContext.AppUsers.FindAsync(id);
        if (user == null) return false;

        user.IsActive = !user.IsActive;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _dbContext.AppUsers.FindAsync(id);
        if (user == null) return false;

        user.IsDeleted = true;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(Guid id, string newPassword)
    {
        var user = await _dbContext.AppUsers.FindAsync(id);
        if (user == null) return false;

        user.PasswordHash = PasswordHasher.HashPassword(newPassword);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public static UserDto MapToDto(AppUser u)
    {
        return new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            Role = u.Role,
            CanViewFinancials = u.CanViewFinancials,
            CanManageExpenses = u.CanManageExpenses,
            CanCancelExpenses = u.CanCancelExpenses,
            CanManageAppointments = u.CanManageAppointments,
            CanManagePatients = u.CanManagePatients,
            CanExportReports = u.CanExportReports,
            CanManageUsers = u.CanManageUsers,
            CanUseQuickActions = u.CanUseQuickActions,
            CanEditPrescriptions = u.CanEditPrescriptions,
            IsActive = u.IsActive,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt
        };
    }
}
