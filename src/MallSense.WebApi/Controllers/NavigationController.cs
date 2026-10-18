using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NavigationController : ControllerBase
{
    private readonly INavigationGraphRepository _graphRepository;
    private readonly IFacilityRepository _facilityRepository;
    private readonly INavigationGraphAdminService _adminService;

    public NavigationController(
        INavigationGraphRepository graphRepository,
        IFacilityRepository facilityRepository,
        INavigationGraphAdminService adminService)
    {
        _graphRepository = graphRepository;
        _facilityRepository = facilityRepository;
        _adminService = adminService;
    }

    /// <summary>
    /// Retrieves all navigation nodes in the mall graph, optionally filtered by floor.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("nodes")]
    [ProducesResponseType(typeof(IReadOnlyList<NavigationNodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NavigationNodeDto>>> GetNodes(
        [FromQuery] int? floor,
        CancellationToken cancellationToken)
    {
        var nodes = await _graphRepository.GetAllNodesAsync(cancellationToken);
        if (floor.HasValue)
        {
            nodes = nodes.Where(n => n.FloorNumber == floor.Value).ToList();
        }
        return Ok(nodes.Select(NavigationNodeDto.FromEntity).ToList());
    }

    /// <summary>
    /// Retrieves all physical navigation edges (connections, distances, accessibility).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("edges")]
    [ProducesResponseType(typeof(IReadOnlyList<NavigationEdgeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NavigationEdgeDto>>> GetEdges(CancellationToken cancellationToken)
    {
        var edges = await _graphRepository.GetAllEdgesAsync(cancellationToken);
        return Ok(edges.Select(NavigationEdgeDto.FromEntity).ToList());
    }

    /// <summary>
    /// Public landmark choices without private graph coordinates or connections.
    /// </summary>
    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyList<NavigationLocationDto>>> GetLocations(
        [FromQuery] int? floor,
        CancellationToken cancellationToken)
    {
        var nodes = await _graphRepository.GetAllNodesAsync(cancellationToken);
        var locations = nodes
            .Where(n => !floor.HasValue || n.FloorNumber == floor.Value)
            .Select(n => new NavigationLocationDto(n.Id, n.FloorNumber, n.Label, n.NodeType, n.Description, n.TenantId))
            .ToList();
        return Ok(locations);
    }

    /// <summary>
    /// Retrieves mall amenities and facilities (Restrooms, Surau, Nursing Room, Concierge).
    /// </summary>
    [HttpGet("facilities")]
    [ProducesResponseType(typeof(IReadOnlyList<MallFacility>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MallFacility>>> GetFacilities(
        [FromQuery] int? floor,
        CancellationToken cancellationToken)
    {
        var facilities = floor.HasValue
            ? await _facilityRepository.GetByFloorAsync(floor.Value, cancellationToken)
            : await _facilityRepository.GetAllAsync(cancellationToken);

        return Ok(facilities);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("nodes")]
    public async Task<ActionResult<NavigationNodeDto>> CreateNode([FromBody] SaveNavigationNodeRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => NavigationNodeDto.FromEntity(await _adminService.CreateNodeAsync(request, cancellationToken)), created: true);

    [Authorize(Roles = "Admin")]
    [HttpPut("nodes/{id:guid}")]
    public async Task<ActionResult<NavigationNodeDto>> UpdateNode(Guid id, [FromBody] SaveNavigationNodeRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => NavigationNodeDto.FromEntity(await _adminService.UpdateNodeAsync(id, request, cancellationToken)));

    [Authorize(Roles = "Admin")]
    [HttpDelete("nodes/{id:guid}")]
    public async Task<IActionResult> DeleteNode(Guid id, CancellationToken cancellationToken)
    {
        try { await _adminService.DeleteNodeAsync(id, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return BadRequest(new { error = ex.Message }); }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("edges")]
    [ProducesResponseType(typeof(NavigationEdgeDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<NavigationEdgeDto>> CreateEdge([FromBody] SaveNavigationEdgeRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => NavigationEdgeDto.FromEntity(await _adminService.CreateEdgeAsync(request, cancellationToken)), created: true);

    [Authorize(Roles = "Admin")]
    [HttpPut("edges/{id:guid}")]
    [ProducesResponseType(typeof(NavigationEdgeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NavigationEdgeDto>> UpdateEdge(Guid id, [FromBody] SaveNavigationEdgeRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => NavigationEdgeDto.FromEntity(await _adminService.UpdateEdgeAsync(id, request, cancellationToken)));

    [Authorize(Roles = "Admin")]
    [HttpDelete("edges/{id:guid}")]
    public async Task<IActionResult> DeleteEdge(Guid id, CancellationToken cancellationToken)
    {
        try { await _adminService.DeleteEdgeAsync(id, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("validate")]
    public async Task<ActionResult<NavigationGraphValidationDto>> ValidateGraph(CancellationToken cancellationToken) =>
        Ok(await _adminService.ValidateAsync(cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPost("ai-draft")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<NavigationAiDraftDto>> GenerateAiDraft(
        [FromForm] NavigationAiDraftForm form,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<NavigationAiDraftRequest>(form.RequestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new ArgumentException("AI draft request is invalid.");
            var files = form.Files ?? [];
            if (files.Count > 8) return BadRequest(new { error = "Upload up to 8 evidence files per turn." });
            if (files.Any(x => x.Length > 10 * 1024 * 1024)) return BadRequest(new { error = "Each evidence file must be 10 MB or smaller." });
            if (files.Sum(x => x.Length) > 50L * 1024 * 1024) return BadRequest(new { error = "Evidence files must total 50 MB or less." });
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp", "image/gif", "application/pdf" };
            if (files.Any(x => !allowed.Contains(x.ContentType))) return BadRequest(new { error = "Use PNG, JPEG, WebP, GIF or PDF evidence." });
            var evidence = new List<NavigationAiEvidenceDto>();
            foreach (var file in files)
            {
                await using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                evidence.Add(new NavigationAiEvidenceDto(file.FileName, file.ContentType, stream.ToArray()));
            }
            return Ok(await _adminService.GenerateAiDraftAsync(request, evidence, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action, bool created = false)
    {
        try
        {
            var result = await action();
            return created ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return BadRequest(new { error = ex.Message }); }
    }
}

public sealed class NavigationAiDraftForm
{
    public string RequestJson { get; set; } = "{}";
    public List<IFormFile>? Files { get; set; }
}
