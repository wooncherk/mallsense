namespace MallSense.Application.DTOs;

public record IntentStatItemDto(string Label, int Count, double Percentage);

public record IntentAnalyticsSummaryDto(
    int TotalInteractions,
    int PeriodDays,
    IReadOnlyList<IntentStatItemDto> TopIntentCategories,
    IReadOnlyList<IntentStatItemDto> TopCategoryInterests,
    IReadOnlyList<IntentStatItemDto> TopRecipientTypes,
    IReadOnlyList<string> EmergingTrends
);

public record DemandCampaignMatchDto(
    string ShopperDemandTrend,
    int DemandIncreasePercentage,
    IReadOnlyList<string> MatchingTenantPromotions,
    string RecommendedAction
);

public record BiInsightReportDto(
    string ExecutiveSummary,
    IReadOnlyList<string> KeyPatterns,
    IReadOnlyList<DemandCampaignMatchDto> DemandMatches,
    IReadOnlyList<string> StrategicRecommendations,
    DateTime GeneratedAtUtc
);

public record DailyBiMetricDto(string Label, int Count);

public record PotentialBiActionDto(
    string Title,
    string Rationale,
    string AffectedArea,
    string ExpectedImpact,
    string Priority,
    bool RequiresApproval = true);

public record DailyBiSummaryDto(
    string Scope,
    string ScopeName,
    string LocalDate,
    DateTime GeneratedAtUtc,
    int TotalSearches,
    int UniqueSessions,
    int DealSearches,
    int EventSearches,
    int ZeroResultSearches,
    int ContentOpens,
    int AskAiContinuations,
    int DirectionRequests,
    double EngagementRate,
    IReadOnlyList<DailyBiMetricDto> TopQueries,
    IReadOnlyList<DailyBiMetricDto> TopContent,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<PotentialBiActionDto> PotentialActions);

public record BiNarrativeDto(
    Guid Id,
    string Scope,
    string ScopeName,
    string LocalDate,
    string SummaryText,
    DateTime GeneratedAtUtc,
    string Trigger);

public record BiSummaryHistoryDto(
    Guid Id,
    string Scope,
    string ScopeName,
    int Days,
    string PeriodLabel,
    string SummaryText,
    DateTime GeneratedAtUtc,
    string Trigger);

public record BiChatTurnDto(string Role, string Message);

public record BiChatRequest(
    string Message,
    IReadOnlyList<BiChatTurnDto>? Conversation = null,
    string? SessionId = null);

public record BiChatResponse(string Answer, DateTime GeneratedAtUtc, string? SessionId = null);

public record BiConversationSummaryDto(
    string SessionId,
    string Title,
    int Days,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int TurnCount,
    string LastMessage);

public record BiConversationHistoryTurnDto(
    Guid Id,
    string Role,
    string Message,
    DateTime CreatedAtUtc);

public record BiConversationHistoryDto(
    string SessionId,
    int Days,
    IReadOnlyList<BiConversationHistoryTurnDto> Turns);
