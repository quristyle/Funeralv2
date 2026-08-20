namespace HelpDeskServer.Dtos;

public record MaintenanceReportDto(
    int Year,
    int Month,
    int TotalRequests,
    int CompletedRequests,
    int UserCompletedRequests,
    int PendingRequests,
    int InProgressRequests,
    int ConsultationRequests,
    int NegotiationRequests,
    double ResolutionRate,
    Dictionary<string, int> RequestsByStatus,
    Dictionary<string, int> RequestsByType,
    double AverageResolutionTimeHours,
    Dictionary<string, int> ResolutionTimeDistribution,
    List<DailyRequestStatDto> DailyStats,
    List<MaintenanceRequestSummaryDto> RecentCompletedItems
);

public record DailyRequestStatDto(int Day, int RequestCount, int CompletedCount);
public record MaintenanceRequestSummaryDto(int Id, string Title, DateTime RequestedAt, DateTime? CompletedAt, string Status, string Type);
