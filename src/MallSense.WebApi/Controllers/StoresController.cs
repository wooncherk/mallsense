using Microsoft.AspNetCore.Mvc;
using MallSense.Application.DTOs;
using MallSense.Application.Exceptions;
using MallSense.Application.Interfaces;
using MallSense.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StoresController : ControllerBase
{
    private readonly IStoreService _storeService;

    public StoresController(IStoreService storeService)
    {
        _storeService = storeService;
    }

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
