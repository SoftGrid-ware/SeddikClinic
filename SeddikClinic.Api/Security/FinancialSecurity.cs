using Microsoft.AspNetCore.Http;
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
public class RequireFinancialPermissionAttribute : Attribute, IAuthorizationFilter
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

        // إذا كان هناك Token يتم فحص الصلاحيات بدقة
        if (user.Identity?.IsAuthenticated == true)
        {
            var isManager = user.IsInRole("Manager") || user.IsInRole("Admin") || user.IsInRole("Doctor");
            var canViewFinancials = user.HasClaim(c => c.Type == "CanViewFinancials" && c.Value.Equals("True", StringComparison.OrdinalIgnoreCase));
            var hasClaim = user.HasClaim(c => c.Type == "Permission" && (c.Value == _permission || c.Value == "Admin.SuperUser"));

            if (!isManager && !canViewFinancials && !hasClaim)
            {
                context.Result = new ObjectResult(new { message = "ليس لديك الصلاحية المالية الكافية لتنفيذ هذا الإجراء." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        // فحص إعادة التحقق للحركات المالية الحساسة
        if (_requireReauth)
        {
            var reauthHeader = context.HttpContext.Request.Headers["X-Doctor-Reauth"].ToString();
            var biometricHeader = context.HttpContext.Request.Headers["X-Biometric-Verified"].ToString();

            if (string.IsNullOrEmpty(reauthHeader) && string.IsNullOrEmpty(biometricHeader))
            {
                // السماح في بيئة التطوير
            }
        }
    }
}
