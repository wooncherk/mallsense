using System.ComponentModel.DataAnnotations;

namespace MallDiscovery.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MallId { get; set; }
    [MaxLength(160)] public required string Name { get; set; }
    [MaxLength(2000)] public required string Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Promotion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(160)] public required string Title { get; set; }
    [MaxLength(2000)] public required string Description { get; set; }
    [MaxLength(2000)] public required string Offer { get; set; }
    [MaxLength(4000)] public string? Terms { get; set; }
    // Inclusive local calendar dates, interpreted in the mall's time zone.
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidUntil { get; set; }
    public PromotionStatus Status { get; set; } = PromotionStatus.Draft;
    public int? MinimumAge { get; set; }
    public int? MaximumAge { get; set; }
    public bool? SuitableForGifting { get; set; }
    public bool? SuitableForCorporateGifting { get; set; }
    public int? AdvanceOrderDays { get; set; }
    [MaxLength(500)] public string? DietaryNotes { get; set; }
    // Optional approximate range, supplied by tenant; never inferred inventory/pricing.
    public int? ApproximateMinimumPriceMinor { get; set; }
    public int? ApproximateMaximumPriceMinor { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
    [MaxLength(200)] public string? ConfirmedBySubject { get; set; }
}

public sealed class PromotionOutlet
{
    public Guid PromotionId { get; set; }
    public Guid PlaceId { get; set; }
}

public sealed class PromotionTag
{
    public Guid PromotionId { get; set; }
    // category, audience, occasion, or interest; values normalized by write services.
    [MaxLength(40)] public required string Kind { get; set; }
    [MaxLength(80)] public required string Value { get; set; }
}

public sealed class PromotionSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? PromotionId { get; set; }
    [MaxLength(500)] public required string AssetStorageKey { get; set; }
    [MaxLength(100)] public required string ContentType { get; set; }
    public ExtractionStatus Status { get; set; } = ExtractionStatus.Pending;
    // Untrusted model output is kept apart from confirmed promotion fields.
    public string? ExtractedDraftJson { get; set; }
    [MaxLength(160)] public string? ModelVersion { get; set; }
    [MaxLength(80)] public string? PromptVersion { get; set; }
    [MaxLength(1000)] public string? FailureReason { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PromotionClarification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    [MaxLength(80)] public required string FieldName { get; set; }
    [MaxLength(1000)] public required string Question { get; set; }
    [MaxLength(2000)] public string? Answer { get; set; }
    public DateTime? AnsweredAtUtc { get; set; }
}
