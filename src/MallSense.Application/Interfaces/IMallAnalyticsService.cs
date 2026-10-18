using MallSense.Application.DTOs;

namespace MallSense.Application.Interfaces;

public interface IMallAnalyticsService
{
    Task<IntentAnalyticsSummaryDto> GetIntentSummaryAsync(int days = 7, CancellationToken cancellationToken = default);
    Task<BiInsightReportDto> GenerateBiInsightsAsync(int days = 7, CancellationToken cancellationToken = default);
    Task<int> SimulateWeekendTrafficSurgeAsync(CancellationToken cancellationToken = default);
    Task<DailyBiSummaryDto> GetDailySummaryAsync(Guid? tenantId = null, int days = 1, int endOffsetDays = 0,
        CancellationToken cancellationToken = default);
    Task<BiNarrativeDto> GenerateDailyNarrativeAsync(Guid? tenantId = null, int days = 1, int endOffsetDays = 0,
        CancellationToken cancellationToken = default);
    Task<BiChatResponse> AskDailySummaryAsync(string message, IReadOnlyList<BiChatTurnDto>? conversation = null,
        Guid? tenantId = null, int days = 1, CancellationToken cancellationToken = default);
}
