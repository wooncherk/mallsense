using System.Text.Json;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Enums;
using MallSense.Domain.Interfaces;

namespace MallSense.UnitTests.Fakes;

public class FakeIntentSignalRepository : IIntentSignalRepository
{
    private readonly List<ShopperIntentSignal> _signals = new();

    public Task<ShopperIntentSignal> AddAsync(ShopperIntentSignal signal, CancellationToken cancellationToken = default)
    {
        _signals.Add(signal);
        return Task.FromResult(signal);
    }

    public Task AddRangeAsync(IEnumerable<ShopperIntentSignal> signals, CancellationToken cancellationToken = default)
    {
        _signals.AddRange(signals);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShopperIntentSignal>> GetRecentSignalsAsync(int days = 7, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShopperIntentSignal>>(_signals.ToList());
    }

    public Task<IReadOnlyList<ShopperIntentSignal>> GetSignalsByCategoryAsync(IntentCategory category, int days = 7, CancellationToken cancellationToken = default)
    {
        var filtered = _signals.Where(s => s.IntentCategory == category).ToList();
        return Task.FromResult<IReadOnlyList<ShopperIntentSignal>>(filtered);
    }
}

public class FakeShopperBehaviorEventRepository : IShopperBehaviorEventRepository
{
    private readonly List<ShopperBehaviorEvent> _events = new();

    public Task<ShopperBehaviorEvent> AddAsync(ShopperBehaviorEvent behaviorEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(behaviorEvent);
        return Task.FromResult(behaviorEvent);
    }

    public Task<IReadOnlyList<ShopperBehaviorEvent>> GetBetweenAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ShopperBehaviorEvent>>(_events
            .Where(item => item.TimestampUtc >= fromUtc && item.TimestampUtc < toUtc)
            .OrderBy(item => item.TimestampUtc)
            .ToList());
}

public class FakeGeminiClient : IGeminiClient
{
    public bool IsLiveApiConfigured => false;
    public Func<string, bool, IReadOnlyList<float>?>? EmbeddingFactory { get; init; }

    public Task<string> GenerateContentAsync(string prompt, string? systemInstruction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Walk past Starbucks and take the escalator up to Level 2. Popular Bookstore is on your right.");
    }

    public async IAsyncEnumerable<string> GenerateContentStreamAsync(string prompt, string? systemInstruction = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return await GenerateContentAsync(prompt, systemInstruction, cancellationToken);
    }

    public Task<string> GenerateJsonAsync(string prompt, string? jsonSchema = null, string? systemInstruction = null, CancellationToken cancellationToken = default)
    {
        if (prompt.Contains("proposed_markdown", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                proposed_markdown = "# Visitor guide\n\nThe north entrance is wheelchair accessible.\n\n## Hours\n\nOpen daily from 10:00 AM to 10:00 PM.",
                rationale = "Added verified opening hours from the supplied evidence.",
                changes = new[] { "Added an Hours section." }
            }));
        }

        if (prompt.Contains("knowledge investigator", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                summary = "A grounded summary of the imported mall information.",
                claims = new[] { "The imported source contains visitor information." },
                questions = new[] { "Which effective date applies to this information?" }
            }));
        }

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            intent = "gift",
            recipient = "8-year-old nephew",
            category_interests = new[] { "toys", "books" },
            excluded_categories = new[] { "fashion" },
            preferences = new[] { "reading", "educational" },
            budget_signal = "mid"
        }));
    }

    public Task<string> AnalyzeImageAsync(byte[] imageBytes, string mimeType, string prompt, CancellationToken cancellationToken = default)
    {
        if (prompt.Contains("floor plan", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                nodes = new[]
                {
                    new { key = "entrance", label = "Main Entrance", type = "Entrance", x = 10, y = 50, description = "Public entrance", confidence = .94 },
                    new { key = "junction", label = "Central Junction", type = "Junction", x = 50, y = 50, description = "Corridor junction", confidence = .88 },
                    new { key = "lift", label = "Lift Lobby", type = "Lift", x = 80, y = 50, description = "Accessible lift", confidence = .91 }
                },
                edges = new[]
                {
                    new { from = "entrance", to = "junction", distance_meters = 35, accessible = true, bidirectional = true, direction_hint = "walk_straight" },
                    new { from = "junction", to = "lift", distance_meters = 25, accessible = true, bidirectional = true, direction_hint = "turn_right" }
                },
                warnings = new[] { "Shop entrances require staff verification." },
                confidence_percentage = 89
            }));
        }

        if (prompt.Contains("knowledge investigator", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                summary = "The image contains mall information that should be verified before publication.",
                claims = new[] { "A mall floor plan was imported." },
                questions = new[] { "Is this the latest approved floor plan?" }
            }));
        }

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            title = "Mid-Autumn Mooncake Heritage Special",
            promotion_type = "Discount",
            offer = "15% off when buying 2 boxes",
            valid_from = DateTime.UtcNow.ToString("yyyy-MM-01"),
            valid_to = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-30"),
            target_audience = "Family and Corporate",
            occasions = "Mid-Autumn Festival",
            dietary_flags = "Halal Certified",
            price_range = "RM 88 - RM 168",
            categories = new[] { "Food & Beverage", "Gifting" },
            missing_information = new[] { "Is advance ordering required?" }
        }));
    }

    public Task<IReadOnlyList<float>?> GenerateEmbeddingAsync(string content, bool isQuery, string? title = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EmbeddingFactory?.Invoke(content, isQuery));
}
