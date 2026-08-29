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

    public async Task<PatientLoginResponseDto> PatientLoginAsync(PatientLoginRequestDto request)
    {
        var rawPhone = request.PhoneNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            return new PatientLoginResponseDto
            {
                Success = false,
                Message = "يرجى إدخال رقم الهاتف."
            };
        }

        var patient = await _dbContext.Patients
            .Include(p => p.Appointments)
            .FirstOrDefaultAsync(p => !p.IsDeleted && (p.PhoneNumber == rawPhone || p.AlternativePhone == rawPhone));

        if (patient == null)
        {
            return new PatientLoginResponseDto
            {
                Success = false,
                Message = "رقم الهاتف غير مسجل لدينا. يرجى إنشاء حساب جديد أو مراجعة الاستقبال."
            };
        }

        // إذا كان المريض مسجلاً من قبل العيادة ولم يقم بتعيين كلمة مرور بعد
        if (string.IsNullOrEmpty(patient.PasswordHash))
        {
            return new PatientLoginResponseDto
            {
                Success = true,
                RequiresPasswordSetup = true,
                Message = "أهلاً بك! يرجى تعيين كلمة مرور لحماية حسابك.",
                Patient = PatientService.MapToDto(patient),
                Token = GeneratePatientJwtToken(patient)
            };
        }

        // التحقق من كلمة المرور
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return new PatientLoginResponseDto
            {
                Success = false,
                Message = "يرجى إدخال كلمة المرور لحسابك."
            };
        }

        if (!PasswordHasher.VerifyPassword(request.Password, patient.PasswordHash))
        {
            return new PatientLoginResponseDto
            {
                Success = false,
                Message = "كلمة المرور غير صحيحة. يرجى المحاولة مرة أخرى."
            };
        }

        return new PatientLoginResponseDto
        {
            Success = true,
            RequiresPasswordSetup = false,
            Message = "تم تسجيل الدخول بنجاح.",
            Patient = PatientService.MapToDto(patient),
            Token = GeneratePatientJwtToken(patient)
        };
    }

    public async Task<PatientLoginResponseDto> RegisterPatientAsync(SeddikClinic.Core.DTOs.Appointments.CreatePatientDto request)
    {
        var rawPhone = request.PhoneNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawPhone) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return new PatientLoginResponseDto
            {
                Success = false,
                Message = "يرجى إدخال الاسم بالكامل ورقم الهاتف."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 4)
        {
            return new PatientLoginResponseDto
            {
                Success = false,
                Message = "يجب أن تكون كلمة المرور 4 أحرف أو أرقام على الأقل."
            };
        }

        var exists = await _dbContext.Patients.AnyAsync(p => !p.IsDeleted && (p.PhoneNumber == rawPhone || p.AlternativePhone == rawPhone));
        if (exists)
        {
            return new PatientLoginResponseDto
            {
                Success = false,
                Message = "رقم الهاتف مسجل بالفعل في النظام. يرجى تسجيل الدخول بدلاً من التسجيل الجديد."
            };
        }

        var count = await _dbContext.Patients.CountAsync();
        var patientCode = $"P-{(count + 1001)}";

        var newPatient = new SeddikClinic.Core.Entities.Appointments.Patient
        {
            PatientCode = patientCode,
            FullName = request.FullName.Trim(),
            PhoneNumber = rawPhone,
            PasswordHash = PasswordHasher.HashPassword(request.Password.Trim()),
            AlternativePhone = request.AlternativePhone?.Trim(),
            NationalId = request.NationalId?.Trim(),
            Gender = request.Gender ?? "ذكر",
            BirthDate = request.BirthDate,
            Age = request.Age,
            Address = request.Address,
            BloodGroup = request.BloodGroup,
            MedicalHistory = request.MedicalHistory,
            Allergies = request.Allergies,
            Notes = request.Notes
        };

        _dbContext.Patients.Add(newPatient);
        await _dbContext.SaveChangesAsync();

        return new PatientLoginResponseDto
        {
            Success = true,
            RequiresPasswordSetup = false,
            Message = "تم إنشاء الحساب بنجاح.",
            Patient = PatientService.MapToDto(newPatient),
            Token = GeneratePatientJwtToken(newPatient)
        };
    }

    public async Task<(bool Success, string Message)> SetPatientPasswordAsync(SetPatientPasswordDto request)
    {
        var patient = await _dbContext.Patients.FindAsync(request.PatientId);
        if (patient == null || patient.IsDeleted)
        {
            return (false, "حساب المريض غير موجود.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
        {
            return (false, "يجب أن تكون كلمة المرور الجديدة 4 أحرف أو أرقام على الأقل.");
        }

        // إذا كان لديه كلمة مرور حالية سابقة، نتحقق منها أولاً
        if (!string.IsNullOrEmpty(patient.PasswordHash) && !string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            if (!PasswordHasher.VerifyPassword(request.CurrentPassword, patient.PasswordHash))
            {
                return (false, "كلمة المرور الحالية غير صحيحة.");
            }
        }

        patient.PasswordHash = PasswordHasher.HashPassword(request.NewPassword.Trim());
        await _dbContext.SaveChangesAsync();

        return (true, "تم تعيين كلمة المرور وحفظها بنجاح.");
    }

    private string GeneratePatientJwtToken(SeddikClinic.Core.Entities.Appointments.Patient patient)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "A_VERY_LONG_SECRET_KEY_FOR_JWT_AUTHENTICATION_SEDDIC_CLINIC_2026_PRODUCTION";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Name, patient.FullName),
            new(ClaimTypes.MobilePhone, patient.PhoneNumber),
            new(ClaimTypes.Role, "Patient")
        };

        var token = new JwtSecurityToken(
            issuer: null,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(90),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
