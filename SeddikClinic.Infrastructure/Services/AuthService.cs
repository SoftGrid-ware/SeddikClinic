using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SeddikClinic.Core.DTOs.Identity;
using SeddikClinic.Core.Entities.Identity;
using SeddikClinic.Core.Enums;
using SeddikClinic.Core.Interfaces;
using SeddikClinic.Infrastructure.Data;

namespace SeddikClinic.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly SeddikClinicDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AuthService(SeddikClinicDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var username = request.Username.Trim().ToLower();
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Username.ToLower() == username);

        if (user == null)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "اسم المستخدم أو كلمة المرور غير صحيحة."
            };
        }

        if (!user.IsActive)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "هذا الحساب تم تجميده من قبل مدير العيادة."
            };
        }

        if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "اسم المستخدم أو كلمة المرور غير صحيحة."
            };
        }

        // تحديث تاريخ آخر دخول
        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var userDto = UserService.MapToDto(user);
        var token = GenerateJwtToken(userDto);

        return new LoginResponseDto
        {
            Success = true,
            Message = "تم تسجيل الدخول بنجاح.",
            Token = token,
            User = userDto
        };
    }

    public string GenerateJwtToken(UserDto user)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "A_VERY_LONG_SECRET_KEY_FOR_JWT_AUTHENTICATION_SEDDIC_CLINIC_2026_PRODUCTION";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("CanViewFinancials", user.CanViewFinancials.ToString()),
            new("CanManageExpenses", user.CanManageExpenses.ToString()),
            new("CanCancelExpenses", user.CanCancelExpenses.ToString()),
            new("CanManageAppointments", user.CanManageAppointments.ToString()),
            new("CanManagePatients", user.CanManagePatients.ToString()),
            new("CanExportReports", user.CanExportReports.ToString()),
            new("CanManageUsers", user.CanManageUsers.ToString())
        };

        if (user.Role == UserRole.Manager)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "Doctor"));
            claims.Add(new Claim("Permission", "Admin.SuperUser"));
            claims.Add(new Claim("Permission", "Financial.ViewDashboard"));
        }

        var token = new JwtSecurityToken(
            issuer: null,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
