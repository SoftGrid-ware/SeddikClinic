using Microsoft.AspNetCore.Mvc;
using SeddikClinic.Core.DTOs.Identity;
using SeddikClinic.Core.Interfaces;

namespace SeddikClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// جلب قائمة جميع مستخدمي المنظومة
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// جلب تفاصيل مستخدم
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound("المستخدم غير موجود.");
        return Ok(user);
    }

    /// <summary>
    /// إنشاء مستخدم جديد (مدير أو مساعد) وتعيين صلاحياته
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.FullName))
        {
            return BadRequest("اسم المستخدم، كلمة المرور، والاسم الكامل حقول مطلوبة.");
        }

        try
        {
            var created = await _userService.CreateUserAsync(dto);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// تعديل مصفوفة صلاحيات المستخدم
    /// </summary>
    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, [FromBody] UpdateUserPermissionsDto dto)
    {
        var success = await _userService.UpdateUserPermissionsAsync(id, dto);
        if (!success) return NotFound("المستخدم غير موجود.");
        return Ok(new { success = true, message = "تم تحديث الصلاحيات بنجاح." });
    }

    /// <summary>
    /// تفعيل أو إيقاف حساب المستخدم
    /// </summary>
    [HttpPut("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var success = await _userService.ToggleUserStatusAsync(id);
        if (!success) return NotFound("المستخدم غير موجود.");
        return Ok(new { success = true, message = "تم تغيير حالة الحساب بنجاح." });
    }

    /// <summary>
    /// حذف مستخدم
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var success = await _userService.DeleteUserAsync(id);
        if (!success) return NotFound("المستخدم غير موجود.");
        return Ok(new { success = true, message = "تم حذف المستخدم بنجاح." });
    }
}
