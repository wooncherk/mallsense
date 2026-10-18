using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MallSense.Application.Interfaces;

namespace MallSense.Application.Services;

/// <summary>
/// Shared semantic scorer for shopper-facing stores, deals, events and published knowledge.
/// Embeddings are cached by content hash; factual filtering remains the responsibility of the
/// calling domain service.
/// </summary>
public sealed class SemanticSearchService(IGeminiClient geminiClient) : ISemanticSearchService
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<float>> DocumentCache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<float>?>>> _queryCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyDictionary<string, double>> ScoreAsync(
        string query,
        IReadOnlyCollection<SemanticSearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || documents.Count == 0)
            return new Dictionary<string, double>();

        var normalizedQuery = query.Trim();
        var queryEmbedding = await _queryCache.GetOrAdd(normalizedQuery, text =>
            new Lazy<Task<IReadOnlyList<float>?>>(
                () => geminiClient.GenerateEmbeddingAsync(text, isQuery: true, cancellationToken: cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if (queryEmbedding is not { Count: > 0 })
            return new Dictionary<string, double>();

        var scored = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(documents, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 4
        }, async (document, token) =>
        {
            var cacheKey = ContentHash(document.Text);
            if (!DocumentCache.TryGetValue(cacheKey, out var documentEmbedding))
            {
                documentEmbedding = await geminiClient.GenerateEmbeddingAsync(
                    document.Text, isQuery: false, document.Title, token);
                if (documentEmbedding is { Count: > 0 }) DocumentCache.TryAdd(cacheKey, documentEmbedding);
            }

            if (documentEmbedding is { Count: > 0 } && documentEmbedding.Count == queryEmbedding.Count)
                scored[document.Id] = CosineSimilarity(queryEmbedding, documentEmbedding);
        });

        return scored;
    }

    private static string ContentHash(string content) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double dot = 0, leftMagnitude = 0, rightMagnitude = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        return leftMagnitude == 0 || rightMagnitude == 0
            ? 0
            : dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
