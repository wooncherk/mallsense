using MallSense.Domain.Enums;

namespace MallSense.Application.DTOs;

public record ShopperChatRequest(
    string? SessionId,
    string Message,
    Guid? CurrentNodeId,
    int? CurrentFloor,
    StructuredIntentDto? PreviousIntent,
    string SurfaceContext = "AskAI",
    IReadOnlyList<ShopperConversationTurnDto>? Conversation = null,
    bool GenerateReply = true,
    string? DisplayMessage = null,
    IReadOnlyList<ShopperChatAttachmentDto>? Attachments = null
);

public record ShopperChatAttachmentDto(string FileName, string MimeType, string Base64Data);

public record ShopperConversationTurnDto(string Role, string Message);

public record ShopperConversationHistoryTurnDto(
    Guid Id,
    string Role,
    string Message,
    DateTime CreatedAtUtc,
    StructuredIntentDto? StructuredIntent,
    IReadOnlyList<RecommendationCardDto> Recommendations,
    IReadOnlyList<ShopperReplySuggestionDto> ReplySuggestions);

public record ShopperReplySuggestionDto(string Label, string Prompt);

public record ShopperConversationHistoryDto(
    string SessionId,
    string SurfaceContext,
    IReadOnlyList<ShopperConversationHistoryTurnDto> Turns,
    StructuredIntentDto? LatestIntent);

public record ShopperConversationSummaryDto(
    string SessionId,
    string Title,
    string SurfaceContext,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int TurnCount,
    string LastMessage);

public record StructuredIntentDto(
    string Intent,
    string? Recipient,
    IReadOnlyList<string> CategoryInterests,
    IReadOnlyList<string> ExcludedCategories,
    IReadOnlyList<string> Preferences,
    string? BudgetSignal,
    string ResultFocus = "Mixed",
    bool NeedsMallSearch = true,
    string? SearchQuery = null
);

public record ShopperChatProgressDto(string Stage, string Title);

public record RecommendationCardDto(
    Guid TenantId,
    string TenantName,
    int FloorNumber,
    string UnitNumber,
    StoreCategory Category,
    string Description,
    string WhyRecommended,
    string? ActivePromotionTitle,
    string? ActivePromotionOffer,
    Guid? NavigationNodeId
);

public record StorefrontSearchRequest(
    string Query,
    int? CurrentFloor = null,
    string? SessionId = null,
    StructuredIntentDto? PreviousIntent = null,
    string SurfaceContext = "Directory");

public record StorefrontSearchResponse(
    string Query,
    string Answer,
    string SessionId,
    StructuredIntentDto StructuredIntent,
    string ResultFocus,
    IReadOnlyList<RecommendationCardDto> Stores,
    IReadOnlyList<KnowledgeSearchResultDto> Knowledge,
    IReadOnlyList<string> SuggestedFollowUps);

public record ShopperBehaviorEventRequest(
    string SessionId,
    string EventType,
    string SurfaceContext = "DealsEvents",
    string? Query = null,
    string? SelectedFilter = null,
    int? ResultCount = null,
    Guid? TargetKnowledgeSourceId = null,
    Guid? TargetTenantId = null,
    string? TargetTitle = null,
    IReadOnlyList<Guid>? RelatedTenantIds = null,
    IReadOnlyList<Guid>? RelatedKnowledgeSourceIds = null);

public record ShopperChatResponse(
    string SessionId,
    string Reply,
    StructuredIntentDto StructuredIntent,
    IReadOnlyList<RecommendationCardDto> Recommendations,
    IReadOnlyList<string> SuggestedFollowUps,
    IReadOnlyList<ShopperReplySuggestionDto>? ReplySuggestions = null
);
