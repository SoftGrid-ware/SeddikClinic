using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SeddikClinic.Api.Security;

public static class FinancialPermissions
{
    public const string ViewDashboard = "Financial.ViewDashboard";     // عرض شاشة الأرباح والمصروفات
    public const string ManageExpenses = "Financial.ManageExpenses";   // إضافة وتعديل وإلغاء المصروفات
    public const string LogExpenseOnly = "Financial.LogExpenseOnly";   // صلاحية تسجيل مصروف فقط للمساعد دون رؤية الأرباح
    public const string ManageBudgets = "Financial.ManageBudgets";     // ضبط الموازنات الشهرية
    public const string ClosePeriod = "Financial.ClosePeriod";         // إقفال الفترات المالية
    public const string ReopenPeriod = "Financial.ReopenPeriod";       // إعادة فتح الفترات المالية (للمدير فقط)
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireFinancialPermissionAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    private readonly string _permission;
    private readonly bool _requireReauth;

    public RequireFinancialPermissionAttribute(string permission, bool requireReauth = false)
    {
        _permission = permission;
        _requireReauth = requireReauth;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            // للتطوير المحلي والتجربة إذا لم تكن التوكن مفعلة يتم السماح، لكن في الإنتاج 401
            var env = context.HttpContext.RequestServices.GetService<IHostEnvironment>();
            if (env != null && env.IsDevelopment())
            {
                return;
            }

            context.Result = new UnauthorizedObjectResult(new { message = "يجب تسجيل الدخول أولاً." });
            return;
        }

        // فحص الصلاحية المطلوبة
        var hasClaim = user.HasClaim(c => c.Type == "Permission" && (c.Value == _permission || c.Value == "Admin.SuperUser"));
        if (!hasClaim && !user.IsInRole("Doctor") && !user.IsInRole("Admin"))
        {
            context.Result = new ObjectResult(new { message = "ليس لديك الصلاحية المالية الكافية لتنفيذ هذا الإجراء." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // فحص إعادة التحقق بالبصمة أو كلمة المرور للحركات المالية الحساسة
        if (_requireReauth)
        {
            var reauthHeader = context.HttpContext.Request.Headers["X-Doctor-Reauth"].ToString();
            var biometricHeader = context.HttpContext.Request.Headers["X-Biometric-Verified"].ToString();

            if (string.IsNullOrEmpty(reauthHeader) && string.IsNullOrEmpty(biometricHeader))
            {
                context.Result = new ObjectResult(new
                {
                    message = "يتطلب هذا الإجراء المالي تأكيد الهوية بالبصمة أو كلمة المرور.",
                    requireReauth = true
                })
                {
                    StatusCode = StatusCodes.Status428PreconditionRequired
                };
            }
        }
    }
}
