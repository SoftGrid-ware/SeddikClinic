using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.DTOs.Identity;
using SeddikClinic.Core.Enums;
using SeddikClinic.Mobile.Shared.Helpers;
using SeddikClinic.Mobile.Shared.Models;

namespace SeddikClinic.Mobile.Shared.Services;

public class MobileApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private string _activeBaseUrl = ApiConfig.BaseUrl;

    public string BaseUrl => _activeBaseUrl;
    public string? AuthToken { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public MobileApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public void SetAuthToken(string? token)
    {
        AuthToken = token;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public void SetBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        _activeBaseUrl = url.TrimEnd('/');
        ApiConfig.BaseUrl = _activeBaseUrl;
    }

    public async Task<bool> CheckConnectionAsync(string? testUrl = null)
    {
        var targetUrl = string.IsNullOrWhiteSpace(testUrl) ? _activeBaseUrl : testUrl.TrimEnd('/');
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var res = await _httpClient.GetAsync($"{targetUrl}/liveness", cts.Token);
            if (res.IsSuccessStatusCode)
            {
                _activeBaseUrl = targetUrl;
                ApiConfig.BaseUrl = targetUrl;
                return true;
            }
        }
        catch
        {
            // fallback probe
        }

        if (string.IsNullOrWhiteSpace(testUrl))
        {
            foreach (var fallback in ApiConfig.FallbackUrls)
            {
                if (fallback == _activeBaseUrl) continue;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var res = await _httpClient.GetAsync($"{fallback}/liveness", cts.Token);
                    if (res.IsSuccessStatusCode)
                    {
                        _activeBaseUrl = fallback;
                        ApiConfig.BaseUrl = fallback;
                        return true;
                    }
                }
                catch { }
            }
        }

        return false;
    }

    private async Task<T?> ExecuteGetAsync<T>(string relativeUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_activeBaseUrl}{relativeUrl}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            LastErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[MobileApiClient GET Error] {relativeUrl}: {ex.Message}");
            
            // Try auto-probing fallback URL on connection failure
            if (await CheckConnectionAsync())
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{_activeBaseUrl}{relativeUrl}");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        return JsonSerializer.Deserialize<T>(json, JsonOptions);
                    }
                }
                catch { }
            }
        }
        return default;
    }

    // =========================================================
    // 🔐 Auth & Login (Admin)
    // =========================================================
    public async Task<LoginResponseDto> LoginAsync(string username, string password)
    {
        try
        {
            var request = new LoginRequestDto { Username = username, Password = password };
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/auth/login", request, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOptions);
                if (result != null && result.Success)
                {
                    SetAuthToken(result.Token);
                    AppSession.UserId = result.User?.Id;
                    AppSession.UserName = result.User?.Username;
                    AppSession.FullName = result.User?.FullName;
                    AppSession.Role = result.User?.Role;
                    AppSession.Token = result.Token;
                    return result;
                }
            }
            var err = await response.Content.ReadAsStringAsync();
            return new LoginResponseDto { Success = false, Message = $"بيانات الدخول غير صحيحة ({response.StatusCode})" };
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            return new LoginResponseDto { Success = false, Message = $"تعذر الاتصال بالسيرفر ({_activeBaseUrl}): {ex.Message}" };
        }
    }

    public void Logout()
    {
        SetAuthToken(null);
        AppSession.Clear();
        PatientSession.Clear();
    }

    // =========================================================
    // 🔐 Patient Auth & Password
    // =========================================================
    public async Task<PatientLoginResponseDto> PatientLoginAsync(string phone, string? password)
    {
        try
        {
            var req = new PatientLoginRequestDto { PhoneNumber = phone.Trim(), Password = password?.Trim() };
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/auth/patient/login", req, JsonOptions);
            var result = await response.Content.ReadFromJsonAsync<PatientLoginResponseDto>(JsonOptions);
            if (result != null)
            {
                if (result.Success && !string.IsNullOrEmpty(result.Token))
                {
                    SetAuthToken(result.Token);
                }
                return result;
            }
            return new PatientLoginResponseDto { Success = false, Message = "تعذر تسجيل الدخول، يرجى مراجعة البيانات." };
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            return new PatientLoginResponseDto { Success = false, Message = $"تعذر الاتصال بالسيرفر ({_activeBaseUrl}): {ex.Message}" };
        }
    }

    public async Task<PatientLoginResponseDto> PatientRegisterAsync(CreatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/auth/patient/register", dto, JsonOptions);
            var result = await response.Content.ReadFromJsonAsync<PatientLoginResponseDto>(JsonOptions);
            if (result != null)
            {
                if (result.Success && !string.IsNullOrEmpty(result.Token))
                {
                    SetAuthToken(result.Token);
                }
                return result;
            }
            return new PatientLoginResponseDto { Success = false, Message = "تعذر إنشاء الحساب." };
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            return new PatientLoginResponseDto { Success = false, Message = $"تعذر الاتصال بالسيرفر ({_activeBaseUrl}): {ex.Message}" };
        }
    }

    public async Task<(bool Success, string Message)> SetPatientPasswordAsync(Guid patientId, string? currentPassword, string newPassword)
    {
        try
        {
            var req = new SetPatientPasswordDto
            {
                PatientId = patientId,
                CurrentPassword = currentPassword?.Trim(),
                NewPassword = newPassword.Trim()
            };
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/auth/patient/set-password", req, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return (true, "تم حفظ كلمة المرور بنجاح ✅");
            }
            var err = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(err) ? "تعذر تغيير كلمة المرور." : err);
        }
        catch (Exception ex)
        {
            return (false, $"خطأ اتصال: {ex.Message}");
        }
    }

    // =========================================================
    // 👤 Patient APIs
    // =========================================================
    public async Task<List<PatientDto>> SearchPatientsAsync(string? query = null, int pageIndex = 1, int pageSize = 50)
    {
        var url = $"/api/patients?pageIndex={pageIndex}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"&query={Uri.EscapeDataString(query)}";
        }
        return await ExecuteGetAsync<List<PatientDto>>(url) ?? new();
    }

    public async Task<PatientDto?> GetPatientByIdAsync(Guid id)
    {
        return await ExecuteGetAsync<PatientDto>($"/api/patients/{id}");
    }

    public async Task<PatientDto?> GetPatientByPhoneAsync(string phone)
    {
        var results = await SearchPatientsAsync(phone, 1, 10);
        return results.FirstOrDefault(p => p.PhoneNumber == phone || (p.AlternativePhone != null && p.AlternativePhone == phone));
    }

    public async Task<PatientDto?> CreatePatientAsync(CreatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/patients", dto, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PatientDto>(JsonOptions);
            }
            var err = await response.Content.ReadAsStringAsync();
            LastErrorMessage = $"HTTP {(int)response.StatusCode}: {err}";
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"خطأ اتصال بالسيرفر ({_activeBaseUrl}): {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[CreatePatient Error]: {ex.Message}");

            // Auto-retry with fallback probe
            if (await CheckConnectionAsync())
            {
                try
                {
                    var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/patients", dto, JsonOptions);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadFromJsonAsync<PatientDto>(JsonOptions);
                    }
                }
                catch { }
            }
        }
        return null;
    }

    public async Task<PatientDto?> UpdatePatientAsync(Guid id, CreatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/patients/{id}", dto, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PatientDto>(JsonOptions);
            }
            LastErrorMessage = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
        }
        return null;
    }

    // =========================================================
    // 📅 Appointments APIs
    // =========================================================
    public async Task<AppointmentSummaryDto?> GetTodayAppointmentsSummaryAsync(Guid? doctorId = null)
    {
        var url = "/api/appointments/today";
        if (doctorId.HasValue) url += $"?doctorId={doctorId.Value}";
        return await ExecuteGetAsync<AppointmentSummaryDto>(url);
    }

    public async Task<List<AppointmentDto>> GetAppointmentsAsync(
        DateTime? date = null,
        Guid? doctorId = null,
        AppointmentStatus? status = null,
        string? searchTerm = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var url = "/api/appointments?";
        if (date.HasValue) url += $"date={date.Value:yyyy-MM-dd}&";
        if (startDate.HasValue) url += $"startDate={startDate.Value:yyyy-MM-dd}&";
        if (endDate.HasValue) url += $"endDate={endDate.Value:yyyy-MM-dd}&";
        if (status.HasValue) url += $"status={(int)status.Value}&";
        if (doctorId.HasValue) url += $"doctorId={doctorId.Value}&";
        if (!string.IsNullOrWhiteSpace(searchTerm)) url += $"searchTerm={Uri.EscapeDataString(searchTerm)}&";

        return await ExecuteGetAsync<List<AppointmentDto>>(url.TrimEnd('&', '?')) ?? new();
    }

    public async Task<List<AppointmentDto>> GetPatientAppointmentsAsync(Guid patientId)
    {
        var all = await GetAppointmentsAsync(searchTerm: null);
        return all.Where(a => a.PatientId == patientId).OrderByDescending(a => a.AppointmentDate).ToList();
    }

    public async Task<AppointmentDto?> CreateAppointmentAsync(CreateAppointmentDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/appointments", dto, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AppointmentDto>(JsonOptions);
            }
            LastErrorMessage = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
        }
        return null;
    }

    public async Task<bool> UpdateAppointmentStatusAsync(Guid id, AppointmentStatus status, string? reason = null)
    {
        try
        {
            var dto = new UpdateAppointmentStatusDto { Status = status, CancellationReason = reason };
            var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/appointments/{id}/status", dto, JsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateAppointmentServiceAsync(Guid id, string serviceType, decimal? totalFees)
    {
        try
        {
            var dto = new UpdateAppointmentServiceDto { ServiceType = serviceType, TotalFees = totalFees };
            var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/appointments/{id}/service", dto, JsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CancelAppointmentAsync(Guid id, string reason = "تم الإلغاء بواسطة المريض")
    {
        return await UpdateAppointmentStatusAsync(id, AppointmentStatus.Cancelled, reason);
    }

    // =========================================================
    // 🩺 Services Catalog
    // =========================================================
    public async Task<List<ClinicServiceDto>> GetAllServicesAsync()
    {
        return await ExecuteGetAsync<List<ClinicServiceDto>>("/api/clinic-services") ?? new();
    }

    // =========================================================
    // 📊 Financial & Expenses (Admin)
    // =========================================================
    public async Task<FinancialDashboardDto?> GetFinancialDashboardAsync(FinancialFilterDto? filter = null)
    {
        filter ??= new FinancialFilterDto();
        var url = $"/api/financial/dashboard?periodType={filter.PeriodType}";
        if (filter.StartDate.HasValue) url += $"&startDate={filter.StartDate.Value:yyyy-MM-dd}";
        if (filter.EndDate.HasValue) url += $"&endDate={filter.EndDate.Value:yyyy-MM-dd}";
        if (filter.DoctorId.HasValue) url += $"&doctorId={filter.DoctorId.Value}";
        if (filter.BranchId.HasValue) url += $"&branchId={filter.BranchId.Value}";

        return await ExecuteGetAsync<FinancialDashboardDto>(url);
    }

    public async Task<List<ExpenseDto>> GetExpensesAsync(ExpenseFilterDto filter)
    {
        var url = $"/api/expenses?pageIndex={filter.PageIndex}&pageSize={filter.PageSize}";
        if (filter.FromDate.HasValue) url += $"&fromDate={filter.FromDate.Value:yyyy-MM-dd}";
        if (filter.ToDate.HasValue) url += $"&toDate={filter.ToDate.Value:yyyy-MM-dd}";
        if (filter.CategoryId.HasValue) url += $"&categoryId={filter.CategoryId.Value}";
        if (filter.DoctorId.HasValue) url += $"&doctorId={filter.DoctorId.Value}";
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm)) url += $"&searchTerm={Uri.EscapeDataString(filter.SearchTerm)}";

        var response = await ExecuteGetAsync<ExpenseListResponseDto>(url);
        return response?.Items ?? new();
    }

    public async Task<List<ExpenseCategoryDto>> GetExpenseCategoriesAsync()
    {
        return await ExecuteGetAsync<List<ExpenseCategoryDto>>("/api/expenses/categories") ?? new();
    }

    public async Task<ExpenseDto?> CreateExpenseAsync(CreateExpenseDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/expenses", dto, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ExpenseDto>(JsonOptions);
            }
            LastErrorMessage = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
        }
        return null;
    }

    public async Task<SeddikClinic.Core.DTOs.Settings.WorkingHoursConfigDto> GetWorkingHoursAsync()
    {
        return await ExecuteGetAsync<SeddikClinic.Core.DTOs.Settings.WorkingHoursConfigDto>("/api/settings/working-hours") ?? new();
    }

    public async Task<bool> UpdateWorkingHoursAsync(SeddikClinic.Core.DTOs.Settings.WorkingHoursConfigDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/settings/working-hours", dto, JsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            return false;
        }
    }
}
