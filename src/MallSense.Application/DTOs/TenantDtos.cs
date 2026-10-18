using MallSense.Domain.Enums;

namespace MallSense.Application.DTOs;

public record PosterExtractionResultDto(
    string Title,
    PromotionType Type,
    string OfferSummary,
    DateTime ValidFrom,
    DateTime ValidTo,
    string TargetAudience,
    string Occasions,
    string? DietaryFlags,
    string? PriceRange,
    IReadOnlyList<string> SuggestedCategories,
    IReadOnlyList<string> MissingInformationQuestions
);

public record PromotionDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string Title,
    PromotionType Type,
    string OfferSummary,
    DateTime ValidFrom,
    DateTime ValidTo,
    string TargetAudience,
    string Occasions,
    string? DietaryFlags,
    string? PriceRange,
    string? AdvanceOrderRequired,
    string? PosterImageUrl,
    int ReadinessScore,
    string ReadinessNotes,
    bool IsActive
);

public record CreatePromotionFromExtractionRequest(
    Guid TenantId,
    PosterExtractionResultDto ExtractedDetails,
    string? PosterImageUrl
);

public record EnrichmentClarificationRequest(
    Guid PromotionId,
    string? SuitableForGifting,
    string? SuitableForCorporate,
    string? AdvanceOrderRequired,
    string? DietaryInformation,
    string? ApproximatePriceRange
);

public record EnrichmentClarificationResponse(
    Guid PromotionId,
    int NewReadinessScore,
    string ReadinessFeedback,
    PromotionDto UpdatedPromotion
);
