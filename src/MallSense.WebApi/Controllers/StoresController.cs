using Microsoft.AspNetCore.Mvc;
using MallSense.Application.DTOs;
using MallSense.Application.Exceptions;
using MallSense.Application.Interfaces;
using MallSense.Domain.Enums;
using MallSense.Application.Services;
using MallSense.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StoresController : ControllerBase
{
    private readonly IStoreService _storeService;
    private readonly TenantSearchAiService _tenantSearchAi;
    private readonly ITenantRepository _tenantRepository;

    public StoresController(IStoreService storeService, TenantSearchAiService tenantSearchAi, ITenantRepository tenantRepository)
    {
        _storeService = storeService;
        _tenantSearchAi = tenantSearchAi;
        _tenantRepository = tenantRepository;
    }

    [HttpPost("{id:guid}/search-validation")]
    [Authorize(Roles = "Admin,TenantStaff")]
    public async Task<ActionResult<TenantSearchValidationResult>> ValidateSearch(Guid id, [FromBody] TenantSearchValidationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessTenantAsync(id, cancellationToken)) return Forbid();
        try { return Ok(await _tenantSearchAi.ValidateAsync(id, request.Description, request.GenerateNewQuestions, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(new { error = "Tenant not found." }); }
        catch (InvalidOperationException ex) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/description-enrichment")]
    [Authorize(Roles = "Admin,TenantStaff")]
    public async Task<ActionResult<TenantDescriptionEnrichmentResult>> EnrichDescription(Guid id, [FromBody] TenantSearchValidationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessTenantAsync(id, cancellationToken)) return Forbid();
        try {
            var result = await _tenantSearchAi.EnrichAsync(id, request.Description, cancellationToken);
            if (result.Description.Length > 1500) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "AI generated a description over 1500 characters. Retry." });
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "Tenant not found." }); }
        catch (InvalidOperationException ex) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message }); }
    }

    private async Task<bool> CanAccessTenantAsync(Guid id, CancellationToken token) =>
        User.IsInRole("Admin") || (User.IsInRole("TenantStaff") &&
            Guid.TryParse(User.FindFirstValue("tenant_id"), out var actorTenantId) && actorTenantId == id &&
            await _tenantRepository.GetByIdAsync(id, token) is not null);

    /// <summary>
    /// Retrieves all stores, optionally filtered by category.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StoreDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StoreDto>>> GetAll(
        [FromQuery] StoreCategory? category,
        CancellationToken cancellationToken)
    {
        var stores = await _storeService.GetAllStoresAsync(category, cancellationToken);
        return Ok(stores);
    }

    /// <summary>
    /// Retrieves a specific store by its unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var store = await _storeService.GetStoreByIdAsync(id, cancellationToken);
            return Ok(store);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Registers a new store in the mall directory.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoreDto>> Create(
        [FromBody] CreateStoreRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _storeService.CreateStoreAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates details of an existing store.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreDto>> Update(
        Guid id,
        [FromBody] UpdateStoreRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _storeService.UpdateStoreAsync(id, request, cancellationToken);
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/storefront-image")]
    [Authorize(Roles = "Admin,TenantStaff")]
    public async Task<ActionResult<StoreDto>> UpdateStorefrontImage(
        Guid id, [FromBody] UpdateStorefrontImageRequest request, CancellationToken cancellationToken)
    {
        if (User.IsInRole("TenantStaff") &&
            (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId) || tenantId != id))
            return Forbid();

        var image = request.StorefrontImageUrl?.Trim() ?? string.Empty;
        if (image.Length == 0 || image.Length > 14_000_000 ||
            !(image.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
              Uri.TryCreate(image, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            return BadRequest(new { error = "Provide a valid image data URL or HTTP(S) image URL." });

        try { return Ok(await _storeService.UpdateStorefrontImageAsync(id, image, cancellationToken)); }
        catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }


    /// <summary>
    /// Deletes a store from the mall directory.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _storeService.DeleteStoreAsync(id, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
