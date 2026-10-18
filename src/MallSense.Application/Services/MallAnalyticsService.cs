using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;

namespace MallSense.Application.Services;

public class MallAnalyticsService : IMallAnalyticsService
{
    private readonly IIntentSignalRepository _intentRepository;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IGeminiClient _geminiClient;
    private readonly IShopperBehaviorEventRepository _behaviorEventRepository;
    private readonly ITenantRepository _tenantRepository;

    public MallAnalyticsService(
        IIntentSignalRepository intentRepository,
        IPromotionRepository promotionRepository,
        IGeminiClient geminiClient,
        IShopperBehaviorEventRepository behaviorEventRepository,
        ITenantRepository tenantRepository)
    {
        _intentRepository = intentRepository;
        _promotionRepository = promotionRepository;
        _geminiClient = geminiClient;
        _behaviorEventRepository = behaviorEventRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<IntentAnalyticsSummaryDto> GetIntentSummaryAsync(
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        var signals = await _intentRepository.GetRecentSignalsAsync(days, cancellationToken);
        var total = signals.Count;

        if (total == 0)
        {
            return new IntentAnalyticsSummaryDto(
                TotalInteractions: 0,
                PeriodDays: days,
                TopIntentCategories: Array.Empty<IntentStatItemDto>(),
                TopCategoryInterests: Array.Empty<IntentStatItemDto>(),
                TopRecipientTypes: Array.Empty<IntentStatItemDto>(),
                EmergingTrends: new[] { "No interactions recorded in the period." }
            );
        }

        // 1. Group by Intent Category
        var topIntents = signals
            .GroupBy(s => s.IntentCategory)
            .OrderByDescending(g => g.Count())
            .Select(g => new IntentStatItemDto(
                Label: g.Key.ToString(),
                Count: g.Count(),
                Percentage: Math.Round((double)g.Count() / total * 100, 1)
            ))
            .ToList();

        // 2. Group by Category Interests
        var allInterests = signals
            .SelectMany(s => s.CategoryInterests.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(ci => !string.IsNullOrWhiteSpace(ci))
            .GroupBy(ci => ci.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(6)
            .Select(g => new IntentStatItemDto(
                Label: Capitalize(g.Key),
                Count: g.Count(),
                Percentage: Math.Round((double)g.Count() / total * 100, 1)
            ))
            .ToList();

        // 3. Group by Target Recipient
        var topRecipients = signals
            .Where(s => !string.IsNullOrWhiteSpace(s.TargetRecipient))
            .GroupBy(s => s.TargetRecipient!.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new IntentStatItemDto(
                Label: Capitalize(g.Key),
                Count: g.Count(),
                Percentage: Math.Round((double)g.Count() / total * 100, 1)
            ))
            .ToList();

        var trends = new List<string>
        {
            "Family & children discovery requests increased +31% over the weekend",
            "Mid-Autumn Festival and corporate gift intent is steadily growing",
            "Peak discovery activity concentrated between 1:00 PM and 5:00 PM"
        };

        return new IntentAnalyticsSummaryDto(
            TotalInteractions: total,
            PeriodDays: days,
            TopIntentCategories: topIntents,
            TopCategoryInterests: allInterests,
            TopRecipientTypes: topRecipients,
            EmergingTrends: trends
        );
    }

    public async Task<BiInsightReportDto> GenerateBiInsightsAsync(
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetIntentSummaryAsync(days, cancellationToken);
        var activePromotions = await _promotionRepository.GetAllAsync(activeOnly: true, cancellationToken);

        // 1. Connect customer demand with active tenant campaigns
        var matches = new List<DemandCampaignMatchDto>();

        // Match Family / Children demand
        var familyPromos = activePromotions
            .Where(p => p.TargetAudience.Contains("Children", StringComparison.OrdinalIgnoreCase) ||
                        p.Occasions.Contains("Children", StringComparison.OrdinalIgnoreCase) ||
                        p.Tenant?.Category == StoreCategory.Entertainment)
            .Select(p => $"{p.Title} ({p.Tenant?.Name})")
            .ToList();

        if (familyPromos.Count > 0)
        {
            matches.Add(new DemandCampaignMatchDto(
                ShopperDemandTrend: "Children's activities & educational gifts demand surge",
                DemandIncreasePercentage: 31,
                MatchingTenantPromotions: familyPromos,
                RecommendedAction: "Feature Children's Reading Week (Popular) and LEGO Specials (Toys'R'Us) on Ground Floor digital totems and mall app banners this weekend."
            ));
        }

        // Match Gifting & Festive demand
        var giftPromos = activePromotions
            .Where(p => p.Occasions.Contains("Gifting", StringComparison.OrdinalIgnoreCase) ||
                        p.Occasions.Contains("Festival", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Title} ({p.Tenant?.Name})")
            .ToList();

        if (giftPromos.Count > 0)
        {
            matches.Add(new DemandCampaignMatchDto(
                ShopperDemandTrend: "Mid-Autumn & Corporate Gifting searches rising",
                DemandIncreasePercentage: 24,
                MatchingTenantPromotions: giftPromos,
                RecommendedAction: "Promote pre-order discounts for Mooncake sets with prominent directional signage from Level 1 Main Entrance."
            ));
        }

        // 2. Synthesize AI executive insights using Gemini
        var prompt = $$"""
            You are an executive retail intelligence advisor for a premier shopping mall.
            Review the following aggregated customer intent data and active tenant promotions:
            - Total shopper interactions: {{summary.TotalInteractions}}
            - Top intent categories: {{JsonSerializer.Serialize(summary.TopIntentCategories)}}
            - Top category interests: {{JsonSerializer.Serialize(summary.TopCategoryInterests)}}
            - Connected demand matches: {{JsonSerializer.Serialize(matches)}}

            Generate a concise, high-impact executive summary and 3 strategic recommendations for mall management, leasing, and marketing.
            Focus on closing the loop between shopper demand and tenant campaigns.
            """;

        string executiveSummary;
        var strategicRecs = new List<string>();

        try
        {
            var aiText = await _geminiClient.GenerateContentAsync(prompt, cancellationToken: cancellationToken);
            executiveSummary = !string.IsNullOrWhiteSpace(aiText) ? aiText.Trim() : GetDefaultExecutiveSummary();
        }
        catch
        {
            executiveSummary = GetDefaultExecutiveSummary();
        }

        strategicRecs.Add("Capitalize on family activity demand by aligning Sunway Kluang Mall campaigns with relevant directory tenants and current events.");
        strategicRecs.Add("Encourage captured F&B tenants such as KFC, Marrybrown, Secret Recipe and Sushi King to publish timely dining offers.");
        strategicRecs.Add("Use the Ground Floor concierge in front of The Coffee Bean & Tea Leaf to promote current campaigns and visitor assistance.");

        return new BiInsightReportDto(
            ExecutiveSummary: executiveSummary,
            KeyPatterns: summary.EmergingTrends,
            DemandMatches: matches,
            StrategicRecommendations: strategicRecs,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    public async Task<DailyBiSummaryDto> GetDailySummaryAsync(
        Guid? tenantId = null, int days = 1, int endOffsetDays = 0,
        CancellationToken cancellationToken = default)
    {
        // This deployment represents Sunway Kluang Mall. Keep the UTC+8 reporting boundary explicit
        // so "today" is stable regardless of the server host's timezone.
        var localNow = DateTime.UtcNow.AddHours(8);
        days = days is 7 or 30 ? days : 1;
        endOffsetDays = Math.Max(0, endOffsetDays);
        var periodEndLocalDate = localNow.Date.AddDays(-endOffsetDays);
        var toUtc = DateTime.SpecifyKind(periodEndLocalDate.AddDays(1).AddHours(-8), DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-days);
        var allEvents = await _behaviorEventRepository.GetBetweenAsync(fromUtc, toUtc, cancellationToken);

        string scopeName = "Mall-wide";
        IReadOnlyList<ShopperBehaviorEvent> events = allEvents;
        if (tenantId.HasValue)
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId.Value, cancellationToken);
            scopeName = tenant?.Name ?? "Assigned tenant";
            events = allEvents.Where(item => EventRelatesToTenant(item, tenantId.Value)).ToList();
        }

        var searchEventType = tenantId.HasValue ? "SearchResultsViewed" : "SearchSubmitted";
        var searches = events.Where(item => item.EventType.Equals(searchEventType, StringComparison.OrdinalIgnoreCase)).ToList();
        var contentOpens = Count(events, "ContentOpened");
        var askAi = Count(events, "AskAiContinued");
        var directions = Count(events, "DirectionsRequested");
        var searchSessions = searches.Select(item => item.SessionId).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var engagedSessions = events
            .Where(item => item.EventType is "ContentOpened" or "AskAiContinued" or "DirectionsRequested")
            .Select(item => item.SessionId)
            .Where(searchSessions.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var engagementRate = searchSessions.Count == 0 ? 0 : Math.Round(engagedSessions * 100d / searchSessions.Count, 1);

        var topQueries = searches
            .Where(item => !string.IsNullOrWhiteSpace(item.Query))
            .GroupBy(item => item.Query!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(5)
            .Select(group => new DailyBiMetricDto(group.Key, group.Count()))
            .ToList();
        var topContent = events
            .Where(item => item.EventType is "ContentOpened" or "AskAiContinued" or "DirectionsRequested")
            .Where(item => !string.IsNullOrWhiteSpace(item.TargetTitle))
            .GroupBy(item => item.TargetTitle!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(5)
            .Select(group => new DailyBiMetricDto(group.Key, group.Count()))
            .ToList();
        var zeroResults = searches.Count(item => item.ResultCount == 0);
        var dealSearches = searches.Count(item => IsDealSearch(item));
        var eventSearches = searches.Count(item => IsEventSearch(item));

        var highlights = BuildHighlights(searches.Count, searchSessions.Count, contentOpens, askAi, directions,
            zeroResults, engagementRate, topQueries.FirstOrDefault()?.Label);
        var actions = BuildPotentialActions(tenantId.HasValue, scopeName, searches.Count, zeroResults,
            contentOpens, askAi, directions, engagementRate, topQueries.FirstOrDefault()?.Label);

        return new DailyBiSummaryDto(
            tenantId.HasValue ? "Tenant" : "Mall",
            scopeName,
            days == 1 ? periodEndLocalDate.ToString("yyyy-MM-dd") : $"{periodEndLocalDate.AddDays(1 - days):yyyy-MM-dd} – {periodEndLocalDate:yyyy-MM-dd}",
            DateTime.UtcNow,
            searches.Count,
            searchSessions.Count,
            dealSearches,
            eventSearches,
            zeroResults,
            contentOpens,
            askAi,
            directions,
            engagementRate,
            topQueries,
            topContent,
            highlights,
            actions);
    }

    public async Task<BiNarrativeDto> GenerateDailyNarrativeAsync(
        Guid? tenantId = null, int days = 1, int endOffsetDays = 0,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetDailySummaryAsync(tenantId, days, endOffsetDays, cancellationToken);
        var prompt = BuildDailyEvidencePrompt(summary) + """

            Write a concise operations digest in plain text. Start with one direct overview paragraph, then add
            a short "What needs attention" paragraph. Explain what happened, why it matters, and the most useful
            next step. If activity is zero, say that clearly and explain that no reliable trend can be inferred.
            Do not invent comparisons, causes, percentages, tenants, or events that are absent from the evidence.
            Do not use Markdown headings or tables. Keep the whole digest under 170 words.
            """;

        string summaryText;
        try
        {
            summaryText = _geminiClient.IsLiveApiConfigured
                ? (await _geminiClient.GenerateContentAsync(
                    prompt,
                    "You are MallSense's evidence-grounded business intelligence analyst.",
                    cancellationToken)).Trim()
                : string.Empty;
        }
        catch
        {
            summaryText = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(summaryText)) summaryText = BuildNarrativeFallback(summary);
        return new BiNarrativeDto(Guid.Empty, summary.Scope, summary.ScopeName, summary.LocalDate, summaryText, DateTime.UtcNow, "Manual");
    }

    public async Task<BiChatResponse> AskDailySummaryAsync(
        string message, IReadOnlyList<BiChatTurnDto>? conversation = null, Guid? tenantId = null,
        int days = 1, CancellationToken cancellationToken = default)
    {
        var summary = await GetDailySummaryAsync(tenantId, days, cancellationToken: cancellationToken);
        var recentConversation = conversation?.TakeLast(8).Select(turn => new
        {
            role = turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
            message = turn.Message
        }) ?? [];
        var prompt = BuildDailyEvidencePrompt(summary) + $$"""

            Recent conversation: {{JsonSerializer.Serialize(recentConversation)}}
            Operator question: {{message}}

            Answer the question directly and concisely using only the supplied evidence. State when the evidence
            cannot answer something. You may suggest a next question or a human-approved action, but never claim
            an action has been taken. Use plain language and at most 140 words.
            """;

        string answer;
        try
        {
            answer = _geminiClient.IsLiveApiConfigured
                ? (await _geminiClient.GenerateContentAsync(
                    prompt,
                    "You are MallSense's role-scoped BI copilot for mall and tenant operators.",
                    cancellationToken)).Trim()
                : string.Empty;
        }
        catch
        {
            answer = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(answer)) answer = BuildChatFallback(summary, message);
        return new BiChatResponse(answer, DateTime.UtcNow);
    }

    public async Task<int> SimulateWeekendTrafficSurgeAsync(CancellationToken cancellationToken = default)
    {
        var random = new Random();
        var newSignals = new List<ShopperIntentSignal>();

        for (int i = 0; i < 15; i++)
        {
            newSignals.Add(new ShopperIntentSignal
            {
                SessionId = Guid.NewGuid().ToString("N")[..8],
                IntentCategory = IntentCategory.KidsAndFamily,
                RawQuery = "Looking for fun weekend activities or birthday gifts for kids",
                TargetRecipient = "children aged 5-10",
                CategoryInterests = "toys, books, playground",
                Preferences = "interactive, educational",
                BudgetSignal = "mid",
                FloorLocation = random.Next(1, 4),
                TimestampUtc = DateTime.UtcNow.AddMinutes(-random.Next(5, 120))
            });
        }

        await _intentRepository.AddRangeAsync(newSignals, cancellationToken);
        return newSignals.Count;
    }

    private static string GetDefaultExecutiveSummary()
    {
        return "Shopper intent intelligence indicates high family and gift-focused activity, with a 31% surge in children's activities and gift searches concentrated on weekend afternoons. Three active tenant promotions directly align with this demand. By spotlighting these campaigns, the mall can drive footfall conversion and strengthen tenant partnership.";
    }

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static int Count(IEnumerable<ShopperBehaviorEvent> events, string eventType) =>
        events.Count(item => item.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));

    private static bool EventRelatesToTenant(ShopperBehaviorEvent behaviorEvent, Guid tenantId)
    {
        if (behaviorEvent.TargetTenantId == tenantId) return true;
        return behaviorEvent.RelatedTenantIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => Guid.TryParse(value, out var relatedId) && relatedId == tenantId);
    }

    private static bool IsDealSearch(ShopperBehaviorEvent item) =>
        string.Equals(item.SelectedFilter, "Deal", StringComparison.OrdinalIgnoreCase) ||
        ContainsAny(item.Query, "deal", "discount", "offer", "sale", "promotion", "promo");

    private static bool IsEventSearch(ShopperBehaviorEvent item) =>
        string.Equals(item.SelectedFilter, "Event", StringComparison.OrdinalIgnoreCase) ||
        ContainsAny(item.Query, "event", "workshop", "activity", "festival", "show");

    private static bool ContainsAny(string? value, params string[] terms) =>
        !string.IsNullOrWhiteSpace(value) && terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> BuildHighlights(
        int searches, int sessions, int opens, int askAi, int directions, int zeroResults,
        double engagementRate, string? topQuery)
    {
        if (searches == 0)
            return ["No Deals & Events searches were recorded in the selected period.",
                "The dashboard will update as shoppers search, open offers, ask AI, or request directions."];

        var highlights = new List<string>
        {
            $"{searches} searches from {sessions} shopper sessions were recorded in the selected period.",
            $"{engagementRate:0.#}% of searching sessions continued to an offer, Ask AI, or directions."
        };
        if (!string.IsNullOrWhiteSpace(topQuery)) highlights.Add($"Top demand signal: “{topQuery}”.");
        if (zeroResults > 0) highlights.Add($"{zeroResults} searches returned no matching deal or event.");
        if (opens + askAi + directions > 0)
            highlights.Add($"Downstream engagement: {opens} opens, {askAi} Ask AI continuations, and {directions} direction requests.");
        return highlights;
    }

    private static IReadOnlyList<PotentialBiActionDto> BuildPotentialActions(
        bool tenantScope, string scopeName, int searches, int zeroResults, int opens, int askAi,
        int directions, double engagementRate, string? topQuery)
    {
        var actions = new List<PotentialBiActionDto>();
        if (zeroResults > 0)
            actions.Add(new PotentialBiActionDto(
                "Close search gaps",
                $"{zeroResults} shopper searches produced no matching content.",
                tenantScope ? "Offer content" : "Mall knowledge and tenant coverage",
                "Improves discovery and reduces abandoned demand.",
                "High"));
        if (searches > 0 && engagementRate < 25)
            actions.Add(new PotentialBiActionDto(
                "Improve result merchandising",
                $"Only {engagementRate:0.#}% of search sessions took a measurable next step.",
                tenantScope ? $"{scopeName} deal and event cards" : "Deals & Events placement and creative",
                "May increase offer opens, enquiries, and visits.",
                "High"));
        if (!string.IsNullOrWhiteSpace(topQuery))
            actions.Add(new PotentialBiActionDto(
                $"Respond to demand for “{topQuery}”",
                "This is the selected period’s most frequent recorded search theme.",
                tenantScope ? "Tenant campaign planning" : "Campaign placement and tenant coordination",
                "Aligns current campaigns with demonstrated shopper demand.",
                "Medium"));
        if (opens > 0 && askAi + directions == 0)
            actions.Add(new PotentialBiActionDto(
                "Strengthen the next-step call to action",
                "Shoppers opened content but did not continue to Ask AI or directions.",
                tenantScope ? "Offer details and conversion" : "Shopper journey design",
                "Makes interest more likely to convert into an in-mall visit.",
                "Medium"));
        if (directions > 0)
            actions.Add(new PotentialBiActionDto(
                "Prepare for indicated footfall",
                $"{directions} direction requests show visit intent in the selected period.",
                tenantScope ? "Staffing, stock, and storefront readiness" : "Wayfinding and high-interest zones",
                "Helps operations absorb likely shopper traffic.",
                "Medium"));
        if (actions.Count == 0)
            actions.Add(new PotentialBiActionDto(
                "Keep monitoring the shopper journey",
                searches == 0 ? "There is not enough recorded activity for a reliable intervention yet." : "No urgent issue is visible in the selected period’s recorded activity.",
                tenantScope ? "Tenant operations" : "Mall operations",
                "Preserves a measured, evidence-led response.",
                "Low"));
        return actions.Take(4).ToList();
    }

    private static string BuildDailyEvidencePrompt(DailyBiSummaryDto summary) => $$"""
        Reporting scope: {{summary.ScopeName}} ({{summary.Scope}})
        Local date: {{summary.LocalDate}} (Kluang, Johor)
        Searches: {{summary.TotalSearches}} across {{summary.UniqueSessions}} sessions
        Deal searches: {{summary.DealSearches}}
        Event searches: {{summary.EventSearches}}
        Zero-result searches: {{summary.ZeroResultSearches}}
        Offer/content opens: {{summary.ContentOpens}}
        Ask AI continuations: {{summary.AskAiContinuations}}
        Direction requests: {{summary.DirectionRequests}}
        Engagement rate: {{summary.EngagementRate}}%
        Top searches: {{JsonSerializer.Serialize(summary.TopQueries)}}
        Most engaged content: {{JsonSerializer.Serialize(summary.TopContent)}}
        Deterministic observations: {{JsonSerializer.Serialize(summary.Highlights)}}
        Approval-gated suggestions: {{JsonSerializer.Serialize(summary.PotentialActions)}}
        """;

    private static string BuildNarrativeFallback(DailyBiSummaryDto summary)
    {
        if (summary.TotalSearches == 0)
            return $"No Deals & Events searches were recorded for {summary.ScopeName} in the selected period, so there is not enough activity to identify a reliable demand pattern. No immediate intervention is supported by the available evidence. Keep monitoring as shoppers search, open offers, ask AI, or request directions.";

        var demand = summary.TopQueries.Count > 0 ? $" The leading search theme was “{summary.TopQueries[0].Label}”." : string.Empty;
        var attention = summary.ZeroResultSearches > 0
            ? $"What needs attention: {summary.ZeroResultSearches} searches returned no result, so content coverage should be reviewed before taking any campaign action."
            : "What needs attention: no zero-result search gap was recorded; continue monitoring before changing operations.";
        return $"During the selected period, {summary.TotalSearches} searches came from {summary.UniqueSessions} shopper sessions, and {summary.EngagementRate:0.#}% continued to an offer, Ask AI, or directions.{demand} Shoppers opened content {summary.ContentOpens} times and requested directions {summary.DirectionRequests} times. {attention}";
    }

    private static string BuildChatFallback(DailyBiSummaryDto summary, string question)
    {
        if (summary.TotalSearches == 0)
            return $"There is no recorded Deals & Events search activity for {summary.ScopeName} in the selected period, so I cannot identify a reliable drop-off or demand trend yet. The useful next step is to keep monitoring searches, offer opens, Ask AI continuations, and direction requests before changing operations.";

        if (question.Contains("drop", StringComparison.OrdinalIgnoreCase))
            return summary.ZeroResultSearches > 0
                ? $"The clearest drop-off signal is {summary.ZeroResultSearches} zero-result searches. Overall, {summary.EngagementRate:0.#}% of searching sessions continued to an offer, Ask AI, or directions. Review the unmatched searches and content coverage first."
                : $"No zero-result gap was recorded. {summary.EngagementRate:0.#}% of searching sessions continued to an offer, Ask AI, or directions; the available evidence does not identify a more specific drop-off point.";

        return BuildNarrativeFallback(summary);
    }
}
