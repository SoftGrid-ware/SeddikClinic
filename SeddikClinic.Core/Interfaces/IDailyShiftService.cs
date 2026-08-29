using SeddikClinic.Core.DTOs.Financial;

namespace SeddikClinic.Core.Interfaces;

public interface IDailyShiftService
{
    Task<DailyShiftSummaryDto> GetCurrentShiftAsync(Guid? branchId = null);
    Task<DailyShiftSummaryDto> OpenShiftAsync(OpenShiftRequestDto dto, string userId, string userName);
    Task<DailyShiftSummaryDto> CloseShiftAsync(CloseShiftRequestDto dto, string userId, string userName);
    Task<List<DailyShiftSummaryDto>> GetShiftHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null);
    Task<DailyShiftSummaryDto> ReopenShiftAsync(Guid shiftId, string userId, string userName, string reason);
}
