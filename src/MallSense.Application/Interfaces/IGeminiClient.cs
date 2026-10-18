namespace MallSense.Application.Interfaces;

public interface IGeminiClient
{
    Task<string> GenerateContentAsync(string prompt, string? systemInstruction = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GenerateContentStreamAsync(string prompt, string? systemInstruction = null, CancellationToken cancellationToken = default);
    Task<string> GenerateJsonAsync(string prompt, string? jsonSchema = null, string? systemInstruction = null, CancellationToken cancellationToken = default);
    Task<string> AnalyzeImageAsync(byte[] imageBytes, string mimeType, string prompt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<float>?> GenerateEmbeddingAsync(string content, bool isQuery, string? title = null,
        CancellationToken cancellationToken = default);
    bool IsLiveApiConfigured { get; }
}
