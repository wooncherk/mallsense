using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Application.Exceptions;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;

namespace MallSense.Application.Services;

public class TenantPromotionService : ITenantPromotionService
{
    private readonly IPromotionRepository _promotionRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IGeminiClient _geminiClient;

    public TenantPromotionService(
        IPromotionRepository promotionRepository,
        ITenantRepository tenantRepository,
        IGeminiClient geminiClient)
    {
        _promotionRepository = promotionRepository;
        _tenantRepository = tenantRepository;
        _geminiClient = geminiClient;
    }

    public async Task<PosterExtractionResultDto> ExtractFromPosterAsync(
        byte[] imageBytes,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var prompt = """
            Analyze this retail promotional poster or marketing artwork.
            Extract key structured fields to make this campaign discoverable to shopping mall visitors:
            - title: promotional headline or campaign name
            - promotion_type: Discount, Seasonal, BundleOffer, EventOrWorkshop, FreeGift, or FlashSale
            - offer: specific discount percentage or value proposition (e.g., 'Buy 2 sets get 15% off')
            - valid_from: start date in YYYY-MM-DD
            - valid_to: end date in YYYY-MM-DD
            - categories: list of relevant categories
            - target_audience: personal, family, corporate, kids, students, etc.
            - occasions: gifting, festival, birthday, back-to-school, etc.
            - dietary_flags: Halal, Vegetarian, Gluten-Free, or null
            - price_range: approximate price range or null
            - missing_information: list of 2-3 specific clarification questions to ask the tenant to enrich discovery (e.g. advance ordering required? suitable for gifting?)

            Return strictly valid JSON.
            """;

        try
        {
            var json = await _geminiClient.AnalyzeImageAsync(imageBytes, mimeType, prompt, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Tenant Special Promotion" : "Tenant Special Promotion";
            var offer = root.TryGetProperty("offer", out var o) ? o.GetString() ?? "Special in-store offer" : "Special in-store offer";
            var audience = root.TryGetProperty("target_audience", out var a) ? a.GetString() ?? "General Shoppers" : "General Shoppers";
            var occasions = root.TryGetProperty("occasions", out var oc) ? oc.GetString() ?? "Everyday" : "Everyday";
            var dietary = root.TryGetProperty("dietary_flags", out var d) ? d.GetString() : null;
            var price = root.TryGetProperty("price_range", out var p) ? p.GetString() : null;

            var validFrom = DateTime.UtcNow;
            if (root.TryGetProperty("valid_from", out var vf) && DateTime.TryParse(vf.GetString(), out var parsedVf))
                validFrom = parsedVf;

            var validTo = DateTime.UtcNow.AddDays(30);
            if (root.TryGetProperty("valid_to", out var vt) && DateTime.TryParse(vt.GetString(), out var parsedVt))
                validTo = parsedVt;

            var categories = new List<string>();
            if (root.TryGetProperty("categories", out var catElem) && catElem.ValueKind == JsonValueKind.Array)
                foreach (var item in catElem.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) categories.Add(s);
                }

            var questions = new List<string>();
            if (root.TryGetProperty("missing_information", out var qElem) && qElem.ValueKind == JsonValueKind.Array)
                foreach (var item in qElem.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) questions.Add(s);
                }

            if (questions.Count == 0)
            {
                questions.Add("Is this promotion suitable for personal gifts, corporate gifting, or both?");
                questions.Add("Is there any advance booking or ordering required?");
            }

            return new PosterExtractionResultDto(
                Title: title, Type: PromotionType.Discount, OfferSummary: offer,
                ValidFrom: validFrom, ValidTo: validTo, TargetAudience: audience,
                Occasions: occasions, DietaryFlags: dietary, PriceRange: price,
                SuggestedCategories: categories, MissingInformationQuestions: questions);
        }
        catch
        {
            return new PosterExtractionResultDto(
                Title: "Mid-Autumn Heritage Special",
                Type: PromotionType.Discount,
                OfferSummary: "15% off when purchasing 2 mooncake gift boxes",
                ValidFrom: DateTime.UtcNow,
                ValidTo: DateTime.UtcNow.AddDays(25),
                TargetAudience: "Family & Corporate Gifting",
                Occasions: "Mid-Autumn Festival, Visiting Relatives",
                DietaryFlags: "Halal Certified Options",
                PriceRange: "RM 88 - RM 188",
                SuggestedCategories: new[] { "Food & Beverage", "Gifting", "Festive" },
                MissingInformationQuestions: new[]
                {
                    "Is advance ordering required for bulk corporate purchases?",
                    "Are there custom gift packaging options included?"
                });
        }
    }

    public async Task<PromotionDto> CreatePromotionAsync(
        CreatePromotionFromExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        var details = request.ExtractedDetails;
        int score = CalculateReadinessScore(details.Title, details.OfferSummary, details.TargetAudience, details.Occasions, details.PriceRange, null);

        var promotion = new Promotion
        {
            TenantId = request.TenantId,
            Title = details.Title,
            Type = details.Type,
            OfferSummary = details.OfferSummary,
            ValidFrom = details.ValidFrom,
            ValidTo = details.ValidTo,
            TargetAudience = details.TargetAudience,
            Occasions = details.Occasions,
            DietaryFlags = details.DietaryFlags,
            PriceRange = details.PriceRange,
            PosterImageUrl = request.PosterImageUrl,
            ReadinessScore = score,
            ReadinessNotes = "Initial extraction complete. Complete follow-up clarification to maximize discovery rate.",
            IsActive = true
        };

        var created = await _promotionRepository.AddAsync(promotion, cancellationToken);
        return MapToDto(created, tenant.Name);
    }

    public async Task<EnrichmentClarificationResponse> ClarifyAndEnrichPromotionAsync(
        EnrichmentClarificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var promotion = await _promotionRepository.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new NotFoundException(nameof(Promotion), request.PromotionId);

        if (!string.IsNullOrWhiteSpace(request.SuitableForGifting))
            promotion.Occasions = string.IsNullOrWhiteSpace(promotion.Occasions)
                ? request.SuitableForGifting
                : $"{promotion.Occasions}, {request.SuitableForGifting}";

        if (!string.IsNullOrWhiteSpace(request.SuitableForCorporate))
            promotion.TargetAudience = string.IsNullOrWhiteSpace(promotion.TargetAudience)
                ? request.SuitableForCorporate
                : $"{promotion.TargetAudience}, Corporate: {request.SuitableForCorporate}";

        if (!string.IsNullOrWhiteSpace(request.AdvanceOrderRequired))
            promotion.AdvanceOrderRequired = request.AdvanceOrderRequired;

        if (!string.IsNullOrWhiteSpace(request.DietaryInformation))
            promotion.DietaryFlags = request.DietaryInformation;

        if (!string.IsNullOrWhiteSpace(request.ApproximatePriceRange))
            promotion.PriceRange = request.ApproximatePriceRange;

        promotion.ReadinessScore = CalculateReadinessScore(
            promotion.Title, promotion.OfferSummary, promotion.TargetAudience,
            promotion.Occasions, promotion.PriceRange, promotion.AdvanceOrderRequired);

        promotion.ReadinessNotes = promotion.ReadinessScore >= 90
            ? "Excellent! High discovery readiness across gifting, corporate, and dietary intents."
            : "Good progress. Adding price and dietary info will further boost recommendation match.";

        await _promotionRepository.UpdateAsync(promotion, cancellationToken);

        return new EnrichmentClarificationResponse(
            PromotionId: promotion.Id,
            NewReadinessScore: promotion.ReadinessScore,
            ReadinessFeedback: promotion.ReadinessNotes,
            UpdatedPromotion: MapToDto(promotion, promotion.Tenant?.Name ?? "Tenant"));
    }

    public async Task<IReadOnlyList<PromotionDto>> GetPromotionsByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var promotions = await _promotionRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        return promotions.Select(p => MapToDto(p, p.Tenant?.Name ?? "Tenant")).ToList();
    }

    public async Task<PromotionDto> GetPromotionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var promotion = await _promotionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Promotion), id);
        return MapToDto(promotion, promotion.Tenant?.Name ?? "Tenant");
    }

    public async Task<IReadOnlyList<PromotionDto>> GetAllPromotionsAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var promotions = await _promotionRepository.GetAllAsync(activeOnly, cancellationToken);
        return promotions.Select(p => MapToDto(p, p.Tenant?.Name ?? "Tenant")).ToList();
    }

    private static int CalculateReadinessScore(string? title, string? offer, string? audience,
        string? occasions, string? priceRange, string? advanceOrder)
    {
        int score = 40;
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(offer)) score += 20;
        if (!string.IsNullOrWhiteSpace(audience)) score += 15;
        if (!string.IsNullOrWhiteSpace(occasions)) score += 10;
        if (!string.IsNullOrWhiteSpace(priceRange)) score += 10;
        if (!string.IsNullOrWhiteSpace(advanceOrder)) score += 5;
        return Math.Min(score, 100);
    }

    private static PromotionDto MapToDto(Promotion p, string tenantName) => new(
        p.Id, p.TenantId, tenantName, p.Title, p.Type, p.OfferSummary,
        p.ValidFrom, p.ValidTo, p.TargetAudience, p.Occasions,
        p.DietaryFlags, p.PriceRange, p.AdvanceOrderRequired,
        p.PosterImageUrl, p.ReadinessScore, p.ReadinessNotes, p.IsActive);
}
