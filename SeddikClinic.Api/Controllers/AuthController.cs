using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Identity;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// تسجيل دخول المستخدم (مدير أو مساعد)
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponseDto
            {
                Success = false,
                Message = "يرجى إدخال اسم المستخدم وكلمة المرور."
            });
        }

        var response = await _authService.LoginAsync(request);
        if (!response.Success)
        {
            return Unauthorized(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// تسجيل دخول المريض بكلمة المرور ورقم الهاتف
    /// </summary>
    [HttpPost("patient/login")]
    public async Task<ActionResult<PatientLoginResponseDto>> PatientLogin([FromBody] PatientLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new PatientLoginResponseDto
            {
                Success = false,
                Message = "يرجى إدخال رقم الهاتف."
            });
        }

        var response = await _authService.PatientLoginAsync(request);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// إنشاء حساب مريض جديد مع كلمة مرور
    /// </summary>
    [HttpPost("patient/register")]
    public async Task<ActionResult<PatientLoginResponseDto>> PatientRegister([FromBody] SeddikClinic.Core.DTOs.Appointments.CreatePatientDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new PatientLoginResponseDto
            {
                Success = false,
                Message = "يرجى إدخال الاسم بالكامل ورقم الهاتف."
            });
        }

        var response = await _authService.RegisterPatientAsync(request);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// تعيين أو تغيير كلمة مرور المريض
    /// </summary>
    [HttpPost("patient/set-password")]
    public async Task<ActionResult> SetPatientPassword([FromBody] SetPatientPasswordDto request)
    {
        if (request.PatientId == Guid.Empty || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { success = false, message = "بيانات غير مكتملة." });
        }

        var result = await _authService.SetPatientPasswordAsync(request);
        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }
}
