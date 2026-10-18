using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Interfaces;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MallController : ControllerBase
{
    private readonly IMallAnalyticsService _analyticsService;
    private readonly IShopperConversationRepository _conversationRepository;

    public MallController(IMallAnalyticsService analyticsService, IShopperConversationRepository conversationRepository)
    {
        _analyticsService = analyticsService;
        _conversationRepository = conversationRepository;
    }

    /// <summary>
    /// Retrieves aggregated customer intent analytics (top intentions, requested categories, target recipients).
    /// </summary>
    [HttpGet("bi/summary")]
    [ProducesResponseType(typeof(IntentAnalyticsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IntentAnalyticsSummaryDto>> GetIntentSummary(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        var summary = await _analyticsService.GetIntentSummaryAsync(days, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Synthesizes GenAI Business Intelligence insights connecting customer demand surges with active tenant campaigns.
    /// Produces actionable executive recommendations for leasing, marketing, and operations.
    /// </summary>
    [HttpGet("bi/insights")]
    [ProducesResponseType(typeof(BiInsightReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BiInsightReportDto>> GetBiInsights(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        var report = await _analyticsService.GenerateBiInsightsAsync(days, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Returns a fast, evidence-backed summary for the current Kluang local day.
    /// Administrators see the mall; tenant staff see only events related to their tenant.
    /// </summary>
    [HttpGet("bi/today")]
    [Authorize(Roles = "Admin,TenantStaff")]
    [ProducesResponseType(typeof(DailyBiSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyBiSummaryDto>> GetTodaySummary(
        [FromQuery] int days = 1, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedRange(days)) return BadRequest(new { message = "Choose a 1, 7, or 30 day range." });
        var tenantId = GetTenantScope();
        if (User.IsInRole("TenantStaff") && tenantId == null) return Forbid();

        return Ok(await _analyticsService.GetDailySummaryAsync(tenantId, days, cancellationToken: cancellationToken));
    }

    /// <summary>Manually generates a plain-language AI digest of today's role-scoped activity.</summary>
    [HttpPost("bi/today/narrative")]
    [Authorize(Roles = "Admin,TenantStaff")]
    [ProducesResponseType(typeof(BiNarrativeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BiNarrativeDto>> GenerateTodayNarrative(
        [FromQuery] int days = 1, [FromQuery] string trigger = "Manual",
        CancellationToken cancellationToken = default)
    {
        if (!IsSupportedRange(days)) return BadRequest(new { message = "Choose a 1, 7, or 30 day range." });
        trigger = trigger.Equals("Scheduled", StringComparison.OrdinalIgnoreCase) ? "Scheduled" : "Manual";
        var tenantId = GetTenantScope();
        if (User.IsInRole("TenantStaff") && tenantId == null) return Forbid();
        var narrative = await _analyticsService.GenerateDailyNarrativeAsync(tenantId, days, cancellationToken: cancellationToken);
        var metadata = JsonSerializer.Serialize(new BiSummaryMetadata(
            narrative.Scope, narrative.ScopeName, days, narrative.LocalDate, trigger));
        var turn = await _conversationRepository.AppendAssistantMessageAsync(
            GetCurrentUserId(), $"bi-{tenantId?.ToString("N") ?? "mall"}-{days}", "BusinessIntelligence",
            narrative.SummaryText, "Summary", metadata, cancellationToken);
        return Ok(narrative with { Id = turn.Id, GeneratedAtUtc = turn.CreatedAtUtc, Trigger = trigger });
    }

    /// <summary>Returns saved AI summaries for the current operator and role scope.</summary>
    [HttpGet("bi/summaries")]
    [Authorize(Roles = "Admin,TenantStaff")]
    [ProducesResponseType(typeof(IReadOnlyList<BiSummaryHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BiSummaryHistoryDto>>> GetSummaryHistory(
        [FromQuery] int? days = null, CancellationToken cancellationToken = default)
    {
        if (days.HasValue && !IsSupportedRange(days.Value))
            return BadRequest(new { message = "Choose a 1, 7, or 30 day range." });
        var items = await ReadSummaryHistory(cancellationToken);
        return Ok(items.Where(item => !days.HasValue || item.Days == days.Value).Take(50).ToList());
    }

    /// <summary>Returns the latest saved summary for the selected range.</summary>
    [HttpGet("bi/summaries/latest")]
    [Authorize(Roles = "Admin,TenantStaff")]
    [ProducesResponseType(typeof(BiSummaryHistoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BiSummaryHistoryDto>> GetLatestSummary(
        [FromQuery] int days = 1, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedRange(days)) return BadRequest(new { message = "Choose a 1, 7, or 30 day range." });
        var latest = (await ReadSummaryHistory(cancellationToken)).FirstOrDefault(item => item.Days == days);
        return latest == null ? NoContent() : Ok(latest);
    }

    /// <summary>Answers an operator's question using only today's role-scoped BI evidence.</summary>
    [HttpPost("bi/today/chat")]
    [Authorize(Roles = "Admin,TenantStaff")]
    [ProducesResponseType(typeof(BiChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BiChatResponse>> AskTodaySummary(
        [FromBody] BiChatRequest request, [FromQuery] int days = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { message = "Enter a question." });
        if (!IsSupportedRange(days)) return BadRequest(new { message = "Choose a 1, 7, or 30 day range." });
        var tenantId = GetTenantScope();
        if (User.IsInRole("TenantStaff") && tenantId == null) return Forbid();
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? $"bi-chat-{days}-{Guid.NewGuid():N}"
            : request.SessionId.Trim();
        if (!sessionId.StartsWith($"bi-chat-{days}-", StringComparison.Ordinal))
            return BadRequest(new { message = "The conversation does not belong to the selected reporting period." });

        var response = await _analyticsService.AskDailySummaryAsync(
            request.Message.Trim(), request.Conversation, tenantId, days, cancellationToken);
        await _conversationRepository.AppendExchangeAsync(
            GetCurrentUserId(), sessionId, "BusinessIntelligenceChat", request.Message.Trim(), response.Answer,
            JsonSerializer.Serialize(new BiChatMetadata(days)), null, null, cancellationToken);
        return Ok(response with { SessionId = sessionId });
    }

    /// <summary>Lists the current operator's saved BI chat sessions for the selected reporting range.</summary>
    [HttpGet("bi/chats")]
    [Authorize(Roles = "Admin,TenantStaff")]
    [ProducesResponseType(typeof(IReadOnlyList<BiConversationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BiConversationSummaryDto>>> GetBiChats(
        [FromQuery] int days = 1, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedRange(days)) return BadRequest(new { message = "Choose a 1, 7, or 30 day range." });
        var conversations = await _conversationRepository.GetAllBySurfaceAsync(
            GetCurrentUserId(), "BusinessIntelligenceChat", cancellationToken);
        return Ok(conversations
            .Where(item => item.SessionId.StartsWith($"bi-chat-{days}-", StringComparison.Ordinal))
            .Select(item =>
            {
                var turns = item.Turns.OrderBy(turn => turn.Sequence).ToList();
                return new BiConversationSummaryDto(item.SessionId,
                    turns.FirstOrDefault(turn => turn.Role == "user")?.Message ?? "New conversation",
                    days, item.CreatedAtUtc, item.UpdatedAtUtc ?? item.CreatedAtUtc, turns.Count,
                    turns.LastOrDefault()?.Message ?? string.Empty);
            }).Take(50).ToList());
    }

    /// <summary>Returns one saved BI chat session owned by the current operator.</summary>
    [HttpGet("bi/chats/{sessionId}")]
    [Authorize(Roles = "Admin,TenantStaff")]
    [ProducesResponseType(typeof(BiConversationHistoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BiConversationHistoryDto>> GetBiChat(
        string sessionId, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetBySessionIdAsync(
            GetCurrentUserId(), sessionId, cancellationToken);
        if (conversation == null || conversation.SurfaceContext != "BusinessIntelligenceChat") return NotFound();
        var days = ParseBiChatDays(sessionId);
        if (!IsSupportedRange(days)) return NotFound();
        var turns = conversation.Turns.OrderBy(turn => turn.Sequence)
            .Select(turn => new BiConversationHistoryTurnDto(turn.Id, turn.Role, turn.Message, turn.CreatedAtUtc))
            .ToList();
        return Ok(new BiConversationHistoryDto(sessionId, days, turns));
    }

    /// <summary>
    /// Simulates weekend discovery traffic surge (for testing and live competition demos).
    /// </summary>
    [HttpPost("bi/simulate-traffic")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SimulateTraffic(CancellationToken cancellationToken)
    {
        var addedCount = await _analyticsService.SimulateWeekendTrafficSurgeAsync(cancellationToken);
        return Ok(new
        {
            message = $"Successfully generated {addedCount} synthetic weekend shopper intent signals.",
            timestampUtc = DateTime.UtcNow
        });
    }

    private Guid? GetTenantScope()
    {
        if (!User.IsInRole("TenantStaff")) return null;
        return Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId) ? tenantId : null;
    }

    private Guid GetCurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Authenticated user id is missing.");

    private async Task<IReadOnlyList<BiSummaryHistoryDto>> ReadSummaryHistory(CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllBySurfaceAsync(
            GetCurrentUserId(), "BusinessIntelligence", cancellationToken);
        var items = new List<BiSummaryHistoryDto>();
        foreach (var turn in conversations.SelectMany(item => item.Turns)
                     .Where(item => item.MessageType == "Summary")
                     .OrderByDescending(item => item.CreatedAtUtc))
        {
            BiSummaryMetadata? metadata = null;
            try { metadata = JsonSerializer.Deserialize<BiSummaryMetadata>(turn.StructuredIntentJson ?? ""); }
            catch (JsonException) { }
            if (metadata == null) continue;
            items.Add(new BiSummaryHistoryDto(turn.Id, metadata.Scope, metadata.ScopeName, metadata.Days,
                metadata.PeriodLabel, turn.Message, turn.CreatedAtUtc, metadata.Trigger));
        }
        return items;
    }

    private static bool IsSupportedRange(int days) => days is 1 or 7 or 30;

    private static int ParseBiChatDays(string sessionId)
    {
        var parts = sessionId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && int.TryParse(parts[2], out var days) ? days : 0;
    }

    private sealed record BiSummaryMetadata(
        string Scope, string ScopeName, int Days, string PeriodLabel, string Trigger);

    private sealed record BiChatMetadata(int Days);
}
