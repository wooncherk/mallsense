using System.Security.Claims;
using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/knowledge")]
[Produces("application/json")]
public class KnowledgeController(IKnowledgeService service) : ControllerBase
{
    private const int MaximumFilesPerBatch = 20;
    private const long MaximumFileBytes = 10 * 1024 * 1024;
    private const long MaximumBatchBytes = 50 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".html", ".yaml", ".yml", ".pdf",
        ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".mp3", ".wav", ".m4a", ".mp4", ".mov"
    };
    [HttpGet("collections")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeCollectionDto>>> Collections(CancellationToken cancellationToken) =>
        Ok(await service.GetCollectionsAsync(Actor(), cancellationToken));

    [HttpGet("sources")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeSourceDto>>> Sources([FromQuery] Guid? collectionId, CancellationToken cancellationToken) =>
        Ok(await service.GetSourcesAsync(Actor(), collectionId, cancellationToken));

    [HttpGet("public-sources")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PublicKnowledgeSourceDto>>> PublicSources(
        [FromQuery] string? kind, CancellationToken cancellationToken) =>
        Ok(await service.GetPublishedSourcesAsync(kind, cancellationToken));

    [HttpPost("import")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<KnowledgeSourceDto>> Import([FromBody] ImportKnowledgeRequest request, CancellationToken cancellationToken) =>
        await Execute(() => service.ImportAsync(request, Actor(), cancellationToken));

    [HttpPost("import-file")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<KnowledgeSourceDto>> ImportFile(
        [FromForm] Guid collectionId, [FromForm] string title, [FromForm] string sourceKind,
        [FromForm] string actionMode, IFormFile file, CancellationToken cancellationToken)
    {
        var validation = ValidateFiles([file]);
        if (validation is not null) return BadRequest(new { error = validation });
        var request = await BuildFileRequest(collectionId, title, sourceKind, actionMode, file, cancellationToken);
        return await Execute(() => service.ImportAsync(request, Actor(), cancellationToken));
    }

    [HttpPost("import-files")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [RequestSizeLimit(55_000_000)]
    public async Task<ActionResult<IReadOnlyList<KnowledgeSourceDto>>> ImportFiles(
        [FromForm] Guid collectionId, [FromForm] string actionMode, [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var validation = ValidateFiles(files);
        if (validation is not null) return BadRequest(new { error = validation });
        var results = new List<KnowledgeSourceDto>();
        foreach (var file in files)
        {
            var title = Path.GetFileNameWithoutExtension(file.FileName);
            var sourceKind = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Image" : "File";
            var request = await BuildFileRequest(collectionId, title, sourceKind, actionMode, file, cancellationToken);
            results.Add(await service.ImportAsync(request, Actor(), cancellationToken));
        }
        return Ok(results);
    }

    [HttpPost("sources/{id:guid}/investigate")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<KnowledgeSourceDto>> Investigate(Guid id, [FromBody] InvestigateKnowledgeRequest request, CancellationToken cancellationToken) =>
        await Execute(() => service.InvestigateAsync(id, request, Actor(), cancellationToken));

    [HttpPut("sources/{id:guid}")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<KnowledgeSourceDto>> UpdateSource(Guid id, [FromBody] UpdateKnowledgeSourceRequest request, CancellationToken cancellationToken) =>
        await Execute(() => service.UpdateSourceAsync(id, request, Actor(), cancellationToken));

    [HttpPut("sources/{id:guid}/status")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<KnowledgeSourceDto>> UpdateStatus(Guid id, [FromBody] UpdateKnowledgeStatusRequest request, CancellationToken cancellationToken) =>
        await Execute(() => service.UpdateStatusAsync(id, request, Actor(), cancellationToken));

    [HttpDelete("sources/{id:guid}")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<IActionResult> DeleteSource(Guid id, CancellationToken cancellationToken)
    {
        try { await service.DeleteSourceAsync(id, Actor(), cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message }); }
    }

    [HttpPost("sources/{id:guid}/suggestions")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [RequestSizeLimit(55_000_000)]
    public async Task<ActionResult<KnowledgeSourceDto>> CreateSuggestion(Guid id, [FromForm] string instruction,
        [FromForm] List<IFormFile>? files, CancellationToken cancellationToken)
    {
        files ??= [];
        var validation = ValidateFiles(files, false);
        if (validation is not null) return BadRequest(new { error = validation });
        if (string.IsNullOrWhiteSpace(instruction)) return BadRequest(new { error = "Describe how AI should enrich the Markdown." });
        var evidence = new List<KnowledgeEvidenceDto>();
        foreach (var file in files) evidence.Add(await BuildEvidence(file, cancellationToken));
        return await Execute(() => service.CreateSuggestionAsync(id, new CreateKnowledgeSuggestionRequest(instruction), evidence, Actor(), cancellationToken: cancellationToken));
    }

    [HttpPost("sources/{id:guid}/suggestions/stream")]
    [Authorize(Roles = "TenantStaff,Admin")]
    [RequestSizeLimit(55_000_000)]
    public async Task CreateSuggestionStream(Guid id, [FromForm] string instruction,
        [FromForm] List<IFormFile>? files, CancellationToken cancellationToken)
    {
        files ??= [];
        var validation = ValidateFiles(files, false);
        if (validation is not null) { Response.StatusCode = StatusCodes.Status400BadRequest; await Response.WriteAsJsonAsync(new { error = validation }, cancellationToken); return; }
        if (string.IsNullOrWhiteSpace(instruction)) { Response.StatusCode = StatusCodes.Status400BadRequest; await Response.WriteAsJsonAsync(new { error = "Describe how AI should enrich the Markdown." }, cancellationToken); return; }
        var evidence = new List<KnowledgeEvidenceDto>();
        foreach (var file in files) evidence.Add(await BuildEvidence(file, cancellationToken));
        Response.ContentType = "application/x-ndjson";
        Response.Headers.CacheControl = "no-cache";
        async Task WriteEvent(object payload, CancellationToken token)
        {
            await Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "\n", token);
            await Response.Body.FlushAsync(token);
        }
        try
        {
            var result = await service.CreateSuggestionAsync(id, new CreateKnowledgeSuggestionRequest(instruction), evidence, Actor(),
                async (progress, token) => await WriteEvent(new { type = "progress", progress }, token), cancellationToken);
            await WriteEvent(new { type = "result", source = result }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await WriteEvent(new { type = "error", error = ex.Message }, cancellationToken);
        }
    }

    [HttpPost("sources/{sourceId:guid}/suggestions/{suggestionId:guid}/accept")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<KnowledgeSourceDto>> AcceptSuggestion(Guid sourceId, Guid suggestionId,
        [FromBody] DecideKnowledgeSuggestionRequest request, CancellationToken cancellationToken) =>
        await Execute(() => service.AcceptSuggestionAsync(sourceId, suggestionId, request, Actor(), cancellationToken));

    [HttpPost("sources/{sourceId:guid}/suggestions/{suggestionId:guid}/reject")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<KnowledgeSourceDto>> RejectSuggestion(Guid sourceId, Guid suggestionId, CancellationToken cancellationToken) =>
        await Execute(() => service.RejectSuggestionAsync(sourceId, suggestionId, Actor(), cancellationToken));

    [HttpPost("sources/{id:guid}/publish")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<KnowledgeSourceDto>> Publish(Guid id, CancellationToken cancellationToken) =>
        await Execute(() => service.PublishAsync(id, Actor(), cancellationToken));

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<KnowledgeSearchResponse>> Search([FromQuery] string query, [FromQuery] bool publishedOnly = true,
        [FromQuery] string[]? kinds = null, CancellationToken cancellationToken = default) =>
        Ok(await service.SearchAsync(query ?? string.Empty, Actor(), publishedOnly, cancellationToken, kinds));

    [HttpPost("sources/{id:guid}/floor-plan-draft")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<FloorPlanDraftDto>> FloorPlanDraft(Guid id, [FromBody] FloorPlanDraftRequest request, CancellationToken cancellationToken) =>
        await Execute(() => service.GenerateFloorPlanDraftAsync(id, request, Actor(), cancellationToken));

    [HttpPost("floor-plan-drafts/{id:guid}/publish")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<FloorPlanDraftDto>> PublishFloorPlan(Guid id, CancellationToken cancellationToken) =>
        await Execute(() => service.PublishFloorPlanDraftAsync(id, Actor(), cancellationToken));

    [HttpPut("floor-plan-drafts/{id:guid}")]
    [Authorize(Roles = "TenantStaff,Admin")]
    public async Task<ActionResult<FloorPlanDraftDto>> UpdateFloorPlan(Guid id, [FromBody] UpdateFloorPlanDraftRequest request, CancellationToken cancellationToken) =>
        await Execute(() => service.UpdateFloorPlanDraftAsync(id, request, Actor(), cancellationToken));

    private KnowledgeActorDto Actor()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Guest";
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "anonymous";
        var tenantValue = User.FindFirstValue("tenant_id");
        return new KnowledgeActorDto(role, Guid.TryParse(tenantValue, out var tenantId) ? tenantId : null, $"User:{subject}");
    }

    private static string? ValidateFiles(IReadOnlyCollection<IFormFile> files, bool requireAtLeastOne = true)
    {
        if (requireAtLeastOne && files.Count == 0) return "Choose at least one file.";
        if (files.Count > MaximumFilesPerBatch) return $"A batch can contain at most {MaximumFilesPerBatch} files.";
        if (files.Sum(x => x.Length) > MaximumBatchBytes) return "The combined batch cannot exceed 50 MB.";
        foreach (var file in files)
        {
            if (file.Length == 0) return $"{file.FileName} is empty.";
            if (file.Length > MaximumFileBytes) return $"{file.FileName} exceeds the 10 MB per-file limit.";
            if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName))) return $"{file.FileName} is not a supported common document, image, audio, or video type.";
        }
        return null;
    }

    private static async Task<KnowledgeEvidenceDto> BuildEvidence(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isText = file.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            extension is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".yaml" or ".yml";
        return new KnowledgeEvidenceDto(file.FileName, file.ContentType, bytes,
            isText ? System.Text.Encoding.UTF8.GetString(bytes) : null);
    }

    private static async Task<ImportKnowledgeRequest> BuildFileRequest(Guid collectionId, string title, string sourceKind,
        string actionMode, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isText = file.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            extension is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".yaml" or ".yml";
        return new ImportKnowledgeRequest(collectionId, title, sourceKind, file.ContentType,
            isText ? System.Text.Encoding.UTF8.GetString(bytes) : null, file.FileName,
            isText ? null : Convert.ToBase64String(bytes), actionMode);
    }

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation)
    {
        try { return Ok(await operation()); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (FormatException) { return BadRequest(new { error = "The imported binary content is invalid." }); }
    }
}
