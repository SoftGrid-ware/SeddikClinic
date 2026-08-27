using SeddikClinic.Core.DTOs.Identity;

namespace SeddikClinic.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    string GenerateJwtToken(UserDto user);
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
