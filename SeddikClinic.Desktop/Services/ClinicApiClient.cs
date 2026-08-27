using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using SeddikClinic.Core.DTOs.Appointments;
using SeddikClinic.Core.DTOs.Financial;
using SeddikClinic.Core.DTOs.Identity;
using SeddikClinic.Core.Enums;

namespace SeddikClinic.Desktop.Services;

public class ClinicApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string[] _endpoints = new[]
    {
        "http://localhost:5000",
        "http://localhost:8080",
        "https://seddikclinic-frinw9km.b4a.run"
    };

    private string _activeBaseUrl = "http://localhost:5000";

    public string BaseUrl => _activeBaseUrl;
    public UserDto? CurrentUser { get; private set; }
    public string? CurrentToken { get; private set; }

    public ClinicApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<bool> IsServerRespondingAsync(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var res = await _httpClient.GetAsync($"{url}/liveness", cts.Token);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnsureServerRunningAsync()
    {
        // 1. فحص إذا كان السيرفر يعمل بالفعل
        if (await IsServerRespondingAsync(_activeBaseUrl))
        {
            return true;
        }

        // 2. فحص الروابط الأخرى
        foreach (var endpoint in _endpoints)
        {
            if (await IsServerRespondingAsync(endpoint))
            {
                _activeBaseUrl = endpoint;
                return true;
            }
        }

        // 3. إذا لم يكن السيرفر يعمل، يتم تشغيله تلقائياً في الخلفية بدون أي نوافذ سوداء!
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var serverExePath = Path.Combine(appDir, "Server", "SeddikClinic.Api.exe");

            if (!File.Exists(serverExePath))
            {
                // مسار التطوير الاحتياطي
                var fallback = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "SeddikClinic_WindowsApp", "Server", "SeddikClinic.Api.exe"));
                if (File.Exists(fallback)) serverExePath = fallback;
            }

            if (File.Exists(serverExePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = serverExePath,
                    Arguments = "--urls \"http://localhost:5000;http://localhost:8080\"",
                    WorkingDirectory = Path.GetDirectoryName(serverExePath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);

                // الانتظار حتى يستيقظ السيرفر
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(500);
                    if (await IsServerRespondingAsync("http://localhost:5000"))
                    {
                        _activeBaseUrl = "http://localhost:5000";
                        return true;
                    }
                }
            }
        }
        catch
        {
            // تجاهل الخطأ والمحاولة عبر الرابط المتاح
        }

        return false;
    }

    public async Task<bool> AutoDetectAndConnectAsync()
    {
        return await EnsureServerRunningAsync();
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<HttpResponseMessage>> action, Action<string>? statusCallback = null)
    {
        var retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(attempt));

        try
        {
            var response = await retryPolicy.ExecuteAsync(action);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
        }
        catch
        {
            await EnsureServerRunningAsync();
            try
            {
                var response = await action();
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<T>(json, JsonOptions);
                }
            }
            catch
            {
                // ignore
            }
        }

        return default;
    }

    // ==========================================
    // 🔐 تسجيل الدخول والمستخدم الحالي (Auth)
    // ==========================================

    public async Task<LoginResponseDto> LoginAsync(string username, string password)
    {
        await EnsureServerRunningAsync();

        var request = new LoginRequestDto { Username = username, Password = password };
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/auth/login", request, JsonOptions);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOptions);
            if (result != null && result.Success)
            {
                CurrentUser = result.User;
                CurrentToken = result.Token;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CurrentToken);
                return result;
            }
        }

        var error = await response.Content.ReadAsStringAsync();
        try
        {
            var errObj = JsonSerializer.Deserialize<LoginResponseDto>(error, JsonOptions);
            if (errObj != null && !string.IsNullOrEmpty(errObj.Message)) return errObj;
        }
        catch { }

        return new LoginResponseDto { Success = false, Message = "اسم المستخدم أو كلمة المرور غير صحيحة." };
    }

    public void Logout()
    {
        CurrentUser = null;
        CurrentToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    // ==========================================
    // 👥 إدارة المستخدمين والصلاحيات (Users Management)
    // ==========================================

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var result = await ExecuteWithRetryAsync<List<UserDto>>(() => _httpClient.GetAsync($"{_activeBaseUrl}/api/users"));
        return result ?? new List<UserDto>();
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/users", dto, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        }
        var error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"فشل إنشاء المستخدم: {error}");
    }

    public async Task<bool> UpdateUserPermissionsAsync(Guid id, UpdateUserPermissionsDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/users/{id}/permissions", dto, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ToggleUserStatusAsync(Guid id)
    {
        var response = await _httpClient.PutAsync($"{_activeBaseUrl}/api/users/{id}/toggle-status", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"{_activeBaseUrl}/api/users/{id}");
        return response.IsSuccessStatusCode;
    }

    // ==========================================
    // 📅 الحجوزات وجدول المواعيد (Appointments)
    // ==========================================

    public async Task<AppointmentSummaryDto?> GetTodayAppointmentsSummaryAsync(Guid? doctorId = null)
    {
        var url = $"{_activeBaseUrl}/api/appointments/today";
        if (doctorId.HasValue) url += $"?doctorId={doctorId.Value}";
        return await ExecuteWithRetryAsync<AppointmentSummaryDto>(() => _httpClient.GetAsync(url));
    }

    public async Task<List<AppointmentDto>> GetAppointmentsAsync(DateTime? date = null, AppointmentStatus? status = null, string? searchTerm = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var url = $"{_activeBaseUrl}/api/appointments?";
        if (date.HasValue) url += $"date={date.Value:yyyy-MM-dd}&";
        if (startDate.HasValue) url += $"startDate={startDate.Value:yyyy-MM-dd}&";
        if (endDate.HasValue) url += $"endDate={endDate.Value:yyyy-MM-dd}&";
        if (status.HasValue) url += $"status={(int)status.Value}&";
        if (!string.IsNullOrWhiteSpace(searchTerm)) url += $"searchTerm={Uri.EscapeDataString(searchTerm)}&";

        var result = await ExecuteWithRetryAsync<List<AppointmentDto>>(() => _httpClient.GetAsync(url.TrimEnd('&', '?')));
        return result ?? new List<AppointmentDto>();
    }

    public async Task<AppointmentDto?> CreateAppointmentAsync(CreateAppointmentDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/appointments", dto, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AppointmentDto>(JsonOptions);
        }
        var error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"فشل حجز الموعد: {error}");
    }

    public async Task<bool> UpdateAppointmentStatusAsync(Guid appointmentId, AppointmentStatus newStatus, string? cancellationReason = null)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/appointments/{appointmentId}/status", new UpdateAppointmentStatusDto
        {
            Status = newStatus,
            CancellationReason = cancellationReason
        }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAppointmentAsync(Guid appointmentId)
    {
        var response = await _httpClient.DeleteAsync($"{_activeBaseUrl}/api/appointments/{appointmentId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAppointmentServiceAsync(Guid appointmentId, string serviceType, decimal? newFees)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/appointments/{appointmentId}/service", new UpdateAppointmentServiceDto
        {
            ServiceType = serviceType,
            TotalFees = newFees
        }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RescheduleAppointmentAsync(Guid appointmentId, DateTime newDate, string newStartTime, int durationMinutes = 30)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/appointments/{appointmentId}/reschedule", new RescheduleAppointmentDto
        {
            NewDate = newDate,
            NewStartTime = newStartTime,
            DurationMinutes = durationMinutes
        }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAppointmentFinancialsAsync(Guid appointmentId, decimal? totalFees, decimal? depositAmount, bool? isDepositPaid)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/appointments/{appointmentId}/financials", new
        {
            TotalFees = totalFees,
            DepositAmount = depositAmount,
            IsDepositPaid = isDepositPaid
        }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PayInstallmentAsync(Guid appointmentId, decimal amount, string? paymentMethod = "نقداً", string? notes = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/appointments/{appointmentId}/pay-installment", new
        {
            Amount = amount,
            PaymentMethod = paymentMethod,
            Notes = notes
        }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    // ==========================================
    // 🩺 كتالوج خدمات وأسعار العيادة (Clinic Services)
    // ==========================================

    public async Task<List<ClinicServiceDto>> GetClinicServicesAsync()
    {
        var result = await ExecuteWithRetryAsync<List<ClinicServiceDto>>(() => _httpClient.GetAsync($"{_activeBaseUrl}/api/clinic-services"));
        return result ?? new List<ClinicServiceDto>();
    }

    public async Task<ClinicServiceDto?> CreateClinicServiceAsync(CreateClinicServiceDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/clinic-services", dto, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ClinicServiceDto>(JsonOptions);
        }
        var error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"فشل إضافة الخدمة: {error}");
    }

    public async Task<ClinicServiceDto?> UpdateClinicServiceAsync(Guid id, UpdateClinicServiceDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/clinic-services/{id}", dto, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ClinicServiceDto>(JsonOptions);
        }
        var error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"فشل تعديل الخدمة: {error}");
    }

    public async Task<bool> DeleteClinicServiceAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"{_activeBaseUrl}/api/clinic-services/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateConsultationPriceAsync(decimal newPrice)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/clinic-services/consultation-price", newPrice, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    // ==========================================
    // 👥 إدارة وسجلات المرضى (Patients)
    // ==========================================

    public async Task<List<PatientDto>> SearchPatientsAsync(string? query = null)
    {
        var url = $"{_activeBaseUrl}/api/patients";
        if (!string.IsNullOrWhiteSpace(query)) url += $"?query={Uri.EscapeDataString(query)}";
        var result = await ExecuteWithRetryAsync<List<PatientDto>>(() => _httpClient.GetAsync(url));
        return result ?? new List<PatientDto>();
    }

    public async Task<PatientDto?> CreatePatientAsync(CreatePatientDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/patients", dto, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PatientDto>(JsonOptions);
        }
        var error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"فشل تسجيل المريض: {error}");
    }

    public async Task<PatientDto?> UpdatePatientAsync(Guid patientId, CreatePatientDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"{_activeBaseUrl}/api/patients/{patientId}", dto, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PatientDto>(JsonOptions);
        }
        var error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"فشل تحديث بيانات المريض: {error}");
    }

    public async Task<PatientDto?> GetPatientByIdAsync(Guid patientId)
    {
        var result = await ExecuteWithRetryAsync<PatientDto>(() => _httpClient.GetAsync($"{_activeBaseUrl}/api/patients/{patientId}"));
        return result;
    }

    public async Task<bool> DeletePatientAsync(Guid patientId)
    {
        var response = await _httpClient.DeleteAsync($"{_activeBaseUrl}/api/patients/{patientId}");
        return response.IsSuccessStatusCode;
    }

    // ==========================================
    // 📊 الأرباح والمصروفات (Financial & Expenses)
    // ==========================================

    public async Task<FinancialDashboardDto?> GetFinancialDashboardAsync(FinancialFilterDto filter, Action<string>? statusCallback = null)
    {
        var queryParams = new List<string>();
        if (filter.DoctorId.HasValue && filter.DoctorId.Value != Guid.Empty) queryParams.Add($"doctorId={filter.DoctorId.Value}");
        if (filter.BranchId.HasValue && filter.BranchId.Value != Guid.Empty) queryParams.Add($"branchId={filter.BranchId.Value}");
        if (!string.IsNullOrWhiteSpace(filter.PeriodType)) queryParams.Add($"periodType={filter.PeriodType}");
        if (filter.StartDate.HasValue) queryParams.Add($"startDate={filter.StartDate.Value:yyyy-MM-dd}");
        if (filter.EndDate.HasValue) queryParams.Add($"endDate={filter.EndDate.Value:yyyy-MM-dd}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var url = $"{_activeBaseUrl}/api/financial/dashboard{queryString}";

        return await ExecuteWithRetryAsync<FinancialDashboardDto>(() => _httpClient.GetAsync(url), statusCallback);
    }

    public async Task<List<ExpenseCategoryDto>> GetExpenseCategoriesAsync()
    {
        var result = await ExecuteWithRetryAsync<List<ExpenseCategoryDto>>(() => _httpClient.GetAsync($"{_activeBaseUrl}/api/expenses/categories"));
        return result ?? new List<ExpenseCategoryDto>();
    }

    public async Task<List<ExpenseDto>> GetExpensesAsync(ExpenseFilterDto filter)
    {
        var pageIndex = filter.PageIndex < 1 ? 1 : filter.PageIndex;
        var pageSize = filter.PageSize < 1 ? 200 : filter.PageSize;

        var queryParams = new List<string>
        {
            $"pageIndex={pageIndex}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(filter.SearchTerm.Trim())}");

        if (filter.CategoryId.HasValue && filter.CategoryId.Value != Guid.Empty)
            queryParams.Add($"categoryId={filter.CategoryId.Value}");

        if (filter.FromDate.HasValue)
            queryParams.Add($"fromDate={filter.FromDate.Value:yyyy-MM-dd}");

        if (filter.ToDate.HasValue)
            queryParams.Add($"toDate={filter.ToDate.Value:yyyy-MM-dd}");

        if (filter.Status.HasValue)
            queryParams.Add($"status={(int)filter.Status.Value}");

        var url = $"{_activeBaseUrl}/api/expenses?{string.Join("&", queryParams)}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new List<ExpenseDto>();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("items", out var itemsElement))
        {
            return JsonSerializer.Deserialize<List<ExpenseDto>>(itemsElement.GetRawText(), JsonOptions) ?? new List<ExpenseDto>();
        }

        return new List<ExpenseDto>();
    }

    public async Task<ExpenseDto?> CreateExpenseAsync(CreateExpenseDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/expenses", dto, JsonOptions);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ExpenseDto>(JsonOptions);
        }
        var error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"فشل إضافة المصروف: {error}");
    }

    public async Task<bool> CancelExpenseAsync(Guid expenseId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/expenses/{expenseId}/cancel", new CancelExpenseDto { CancellationReason = reason }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteExpenseAsync(Guid expenseId)
    {
        var response = await _httpClient.DeleteAsync($"{_activeBaseUrl}/api/expenses/{expenseId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<ExpenseCategoryDto?> CreateExpenseCategoryAsync(string nameAr, bool isDirectCost = false)
    {
        var response = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/expenses/categories", new { NameAr = nameAr, IsDirectCost = isDirectCost }, JsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ExpenseCategoryDto>(JsonOptions);
    }

    public async Task<byte[]> ExportExpensesExcelAsync()
    {
        var response = await _httpClient.GetAsync($"{_activeBaseUrl}/api/financial/export/excel");
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<SeddikClinic.Core.DTOs.Settings.WorkingHoursConfigDto> GetWorkingHoursAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<SeddikClinic.Core.DTOs.Settings.WorkingHoursConfigDto>($"{_activeBaseUrl}/api/settings/working-hours", JsonOptions);
            return res ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<bool> UpdateWorkingHoursAsync(SeddikClinic.Core.DTOs.Settings.WorkingHoursConfigDto dto)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync($"{_activeBaseUrl}/api/settings/working-hours", dto, JsonOptions);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
