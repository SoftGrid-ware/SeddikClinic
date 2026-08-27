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
}
