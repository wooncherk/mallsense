using System.Security.Cryptography;
using System.Text;

namespace MallSense.Application.DTOs;

public sealed record TenantSearchValidationRequest(string? Description = null, bool GenerateNewQuestions = false);
public sealed record TenantSearchValidationQuestion(string Question, int? TenantRank, bool IsRelevant, string Assessment);
public sealed record TenantSearchValidationResult(int Rating, string Summary, IReadOnlyList<TenantSearchValidationQuestion> Questions, DateTime ValidatedAtUtc, string DescriptionHash);
public sealed record TenantDescriptionEnrichmentResult(string Description, IReadOnlyList<string> Changes);

public static class TenantSearchValidationHash
{
    public static string Compute(string? description) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(description?.Trim() ?? string.Empty)));
}
