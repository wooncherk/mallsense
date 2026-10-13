using MallSense.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MallSense.Pages;

public sealed class IndexModel(HomeCatalog catalog) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    public CatalogSnapshot? Catalog { get; private set; }
    public IReadOnlyList<PlaceSummary> Results { get; private set; } = [];
    public string[] Categories { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Catalog = await catalog.ReadAsync(cancellationToken);
        if (Catalog is null) return;
        Query = Query?.Trim();
        if (Query?.Length > 200) Query = Query[..200];
        Categories = Catalog.Places.Select(p => p.Category).Distinct().Order().ToArray();
        var candidates = Catalog.Places.Where(p => string.IsNullOrEmpty(Category) || p.Category == Category);
        if (!string.IsNullOrWhiteSpace(Query))
        {
            // Honest, deterministic prototype matching; no generated factual claims.
            var words = Query.ToLowerInvariant().Split([' ', ',', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !new[] { "the", "for", "with", "something", "find", "need", "somewhere", "who", "and" }.Contains(w))
                .Select(w => w switch { "gifts" => "gift", "children" or "nephew" => "child", "eat" or "food" => "dining", "activities" => "activity", _ => w }).Distinct().ToArray();
            Results = candidates.Select(p => new { Place = p, Score = words.Count(w => (p.Name + " " + p.Description + " " + p.Category + " " + string.Join(' ', p.Tags)).Contains(w, StringComparison.OrdinalIgnoreCase)) })
                .Where(p => p.Score > 0).OrderByDescending(p => p.Score).ThenBy(p => p.Place.Name).Select(p => p.Place).ToList();
        }
        else Results = candidates.ToList();
    }
}
