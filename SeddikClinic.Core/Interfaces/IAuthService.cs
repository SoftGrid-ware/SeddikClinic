using SeddikClinic.Core.DTOs.Identity;

namespace SeddikClinic.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    string GenerateJwtToken(UserDto user);

    Task<PatientLoginResponseDto> PatientLoginAsync(PatientLoginRequestDto request);
    Task<PatientLoginResponseDto> RegisterPatientAsync(SeddikClinic.Core.DTOs.Appointments.CreatePatientDto request);
    Task<(bool Success, string Message)> SetPatientPasswordAsync(SetPatientPasswordDto request);
}

public interface IUserService
{
    Task<List<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(Guid id);
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task<bool> UpdateUserPermissionsAsync(Guid id, UpdateUserPermissionsDto dto);
    Task<bool> ToggleUserStatusAsync(Guid id);
    Task<bool> DeleteUserAsync(Guid id);
    Task<bool> ResetPasswordAsync(Guid id, string newPassword);
}
