using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MallSense.Application.DTOs;
using MallSense.Application.Exceptions;
using MallSense.Application.Interfaces;
using System.Security.Claims;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TenantController : ControllerBase
{
    private readonly ITenantPromotionService _promotionService;

    public TenantController(ITenantPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    /// <summary>
    /// Analyzes an uploaded promotional poster or artwork using Gemini Vision.
    /// Extracts structured promotional data and detects missing context fields.
    /// </summary>
    [HttpPost("promotions/upload-poster")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PosterExtractionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosterExtractionResultDto>> UploadPoster(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "Please provide a valid image file (PNG, JPG, or WebP)." });
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var result = await _promotionService.ExtractFromPosterAsync(bytes, file.ContentType, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Simulates/processes poster upload via base64 JSON payload (for frontend clients).
    /// </summary>
    [HttpPost("promotions/extract-base64")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [ProducesResponseType(typeof(PosterExtractionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PosterExtractionResultDto>> ExtractBase64(
        [FromBody] PosterUploadPayload payload,
        CancellationToken cancellationToken)
    {
        var bytes = !string.IsNullOrWhiteSpace(payload.Base64Data)
            ? Convert.FromBase64String(payload.Base64Data)
            : Array.Empty<byte>();

        var result = await _promotionService.ExtractFromPosterAsync(bytes, payload.MimeType ?? "image/jpeg", cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Confirms and saves extracted promotion details into the mall knowledge base.
    /// </summary>
    [HttpPost("promotions/confirm")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionDto>> ConfirmPromotion(
        [FromBody] CreatePromotionFromExtractionRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanAccessTenant(request.TenantId)) return Forbid();

        try
        {
            var created = await _promotionService.CreatePromotionAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetPromotionById), new { id = created.Id }, created);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Guided conversational clarification to enrich promotion details and boost discovery readiness.
    /// </summary>
    [HttpPost("promotions/clarify")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [ProducesResponseType(typeof(EnrichmentClarificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrichmentClarificationResponse>> ClarifyPromotion(
        [FromBody] EnrichmentClarificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var promotion = await _promotionService.GetPromotionByIdAsync(request.PromotionId, cancellationToken);
            if (!CanAccessTenant(promotion.TenantId)) return Forbid();

            var response = await _promotionService.ClarifyAndEnrichPromotionAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all promotions for a specific tenant.
    /// </summary>
    [HttpGet("promotions/tenant/{tenantId:guid}")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<PromotionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> GetPromotionsByTenant(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!CanAccessTenant(tenantId)) return Forbid();

        var list = await _promotionService.GetPromotionsByTenantAsync(tenantId, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Retrieves all active enriched promotions in the mall.
    /// </summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(IReadOnlyList<PromotionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> GetAllPromotions(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var list = await _promotionService.GetAllPromotionsAsync(activeOnly, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific promotion by ID.
    /// </summary>
    [HttpGet("promotions/{id:guid}")]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionDto>> GetPromotionById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var list = await _promotionService.GetAllPromotionsAsync(activeOnly: false, cancellationToken);
        var item = list.FirstOrDefault(p => p.Id == id);
        if (item == null)
        {
            return NotFound(new { error = $"Promotion with ID '{id}' was not found." });
        }
        return Ok(item);
    }

    private bool CanAccessTenant(Guid tenantId) =>
        User.IsInRole("Admin") ||
        User.IsInRole("TenantStaff") &&
        Guid.TryParse(User.FindFirstValue("tenant_id"), out var actorTenantId) &&
        actorTenantId == tenantId;
}

public record PosterUploadPayload(string? Base64Data, string? MimeType);
