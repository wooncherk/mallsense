using System.Security.Claims;
using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IShopperConversationRepository _conversationRepository;

    public UsersController(IAuthService authService, IShopperConversationRepository conversationRepository)
    {
        _authService = authService;
        _conversationRepository = conversationRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ManagedUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ManagedUserDto>>> GetUsers(CancellationToken cancellationToken)
        => Ok(await _authService.GetUsersAsync(cancellationToken));

    [HttpGet("{id:guid}/conversations")]
    [ProducesResponseType(typeof(IReadOnlyList<ShopperConversationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShopperConversationSummaryDto>>> GetConversations(
        Guid id,
        CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(id, cancellationToken);
        return Ok(conversations.Select(ToSummaryDto).ToList());
    }

    [HttpGet("{id:guid}/conversations/{sessionId}")]
    [ProducesResponseType(typeof(ShopperConversationHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopperConversationHistoryDto>> GetConversation(
        Guid id,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetBySessionIdAsync(id, sessionId, cancellationToken);
        return conversation == null ? NotFound() : Ok(ToHistoryDto(conversation));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagedUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManagedUserDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _authService.CreateUserAsync(request, cancellationToken);
            return Created($"/api/users/{created.Id}", created);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = ex.Message });
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/access")]
    [ProducesResponseType(typeof(ManagedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagedUserDto>> UpdateAccess(
        Guid id,
        [FromBody] UpdateUserAccessRequest request,
        CancellationToken cancellationToken)
    {
        var callerIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(callerIdValue, out var callerId) && callerId == id &&
            !string.Equals(request.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "You cannot remove your own administrator access." });

        try
        {
            return Ok(await _authService.UpdateUserAccessAsync(id, request, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static ShopperConversationSummaryDto ToSummaryDto(ShopperConversation conversation)
    {
        var turns = conversation.Turns.OrderBy(turn => turn.Sequence).ToList();
        var title = turns.FirstOrDefault(turn => turn.Role == "user")?.Message ?? "New conversation";
        var lastMessage = turns.LastOrDefault()?.Message ?? string.Empty;
        return new ShopperConversationSummaryDto(
            conversation.SessionId,
            Truncate(title, 54),
            conversation.SurfaceContext,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc ?? conversation.CreatedAtUtc,
            turns.Count,
            Truncate(lastMessage, 90));
    }

    private static ShopperConversationHistoryDto ToHistoryDto(ShopperConversation conversation)
    {
        var turns = conversation.Turns.OrderBy(turn => turn.Sequence).Select(turn =>
            new ShopperConversationHistoryTurnDto(
                turn.Id,
                turn.Role,
                turn.Message,
                turn.CreatedAtUtc,
                Deserialize<StructuredIntentDto>(turn.StructuredIntentJson),
                Deserialize<IReadOnlyList<RecommendationCardDto>>(turn.RecommendationsJson) ?? [],
                Deserialize<IReadOnlyList<ShopperReplySuggestionDto>>(turn.ReplySuggestionsJson) ?? [])).ToList();
        var latestIntent = turns.LastOrDefault(turn => turn.StructuredIntent != null)?.StructuredIntent;
        return new ShopperConversationHistoryDto(conversation.SessionId, conversation.SurfaceContext, turns, latestIntent);
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return default; }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..(maxLength - 1)].TrimEnd()}…";
}
