namespace MallSense.Application.Interfaces;

public record SemanticSearchDocument(string Id, string Text, string? Title = null);

public interface ISemanticSearchService
{
    Task<IReadOnlyDictionary<string, double>> ScoreAsync(
        string query,
        IReadOnlyCollection<SemanticSearchDocument> documents,
        CancellationToken cancellationToken = default);
}
