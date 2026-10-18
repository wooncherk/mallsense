using System.Text.Json;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;

namespace MallSense.WebApi.Services;

/// <summary>Creates one saved daily BI summary per operator at Kluang local midnight.</summary>
public sealed class BiSummaryScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<BiSummaryScheduler> logger) : BackgroundService
{
    private static readonly TimeZoneInfo KualaLumpurTimeZone = ResolveKualaLumpurTimeZone();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await GenerateMissingDailySummaries(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var utcNow = DateTime.UtcNow;
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, KualaLumpurTimeZone);
            var nextLocalMidnight = localNow.Date.AddDays(1);
            var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, KualaLumpurTimeZone);
            await Task.Delay(nextUtc - utcNow, stoppingToken);
            await GenerateMissingDailySummaries(stoppingToken);
        }
    }

    private async Task GenerateMissingDailySummaries(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var users = await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetAllAsync(cancellationToken);
            var analytics = scope.ServiceProvider.GetRequiredService<IMallAnalyticsService>();
            var conversations = scope.ServiceProvider.GetRequiredService<IShopperConversationRepository>();
            var localDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KualaLumpurTimeZone)
                .Date.AddDays(-1).ToString("yyyy-MM-dd");

            foreach (var user in users.Where(item => item.Role is UserRole.Admin or UserRole.TenantStaff))
            {
                var tenantId = user.Role == UserRole.TenantStaff ? user.TenantId : null;
                if (user.Role == UserRole.TenantStaff && tenantId == null) continue;
                var sessionId = $"bi-{tenantId?.ToString("N") ?? "mall"}-1";
                var existing = await conversations.GetBySessionIdAsync(user.Id, sessionId, cancellationToken);
                if (existing?.Turns.Any(turn => turn.MessageType == "Summary" &&
                    ReadPeriodLabel(turn.StructuredIntentJson) == localDate) == true) continue;

                var narrative = await analytics.GenerateDailyNarrativeAsync(
                    tenantId, 1, endOffsetDays: 1, cancellationToken: cancellationToken);
                var metadata = JsonSerializer.Serialize(new SummaryMetadata(
                    narrative.Scope, narrative.ScopeName, 1, narrative.LocalDate, "Scheduled"));
                await conversations.AppendAssistantMessageAsync(user.Id, sessionId, "BusinessIntelligence",
                    narrative.SummaryText, "Summary", metadata, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to generate scheduled BI summaries.");
        }
    }

    private static string? ReadPeriodLabel(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<SummaryMetadata>(json)?.PeriodLabel; }
        catch (JsonException) { return null; }
    }

    private static TimeZoneInfo ResolveKualaLumpurTimeZone()
    {
        foreach (var id in new[] { "Asia/Kuala_Lumpur", "Singapore Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Kuala Lumpur", TimeSpan.FromHours(8), "Kuala Lumpur", "Kuala Lumpur");
    }

    private sealed record SummaryMetadata(
        string Scope, string ScopeName, int Days, string PeriodLabel, string Trigger);
}
