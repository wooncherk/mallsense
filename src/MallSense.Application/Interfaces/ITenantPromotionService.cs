using MallSense.Application.DTOs;

namespace MallSense.Application.Interfaces;

public interface ITenantPromotionService
{
    Task<PosterExtractionResultDto> ExtractFromPosterAsync(byte[] imageBytes, string mimeType, CancellationToken cancellationToken = default);
    Task<PromotionDto> CreatePromotionAsync(CreatePromotionFromExtractionRequest request, CancellationToken cancellationToken = default);
    Task<EnrichmentClarificationResponse> ClarifyAndEnrichPromotionAsync(EnrichmentClarificationRequest request, CancellationToken cancellationToken = default);
    Task<PromotionDto> GetPromotionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromotionDto>> GetPromotionsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromotionDto>> GetAllPromotionsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
}
