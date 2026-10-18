using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MallSense.Application.Interfaces;
using Google.GenAI;
using Google.GenAI.Types;

namespace MallSense.Infrastructure.AI;

public class GeminiApiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _modelName;
    private readonly bool _allowSimulationFallback;
    private readonly string _embeddingModel;
    private readonly Client? _embeddingClient;
    private readonly ILogger<GeminiApiClient> _logger;

    public bool IsLiveApiConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public GeminiApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["Gemini:ApiKey"];

        _modelName = configuration["Gemini:Model"]
            ?? throw new InvalidOperationException("Gemini:Model must be configured in appsettings.json.");
        _embeddingModel = configuration["Gemini:EmbeddingModel"]
            ?? throw new InvalidOperationException("Gemini:EmbeddingModel must be configured in appsettings.json.");
        _allowSimulationFallback = bool.TryParse(configuration["Gemini:AllowSimulationFallback"], out var allowFallback) && allowFallback;
        _embeddingClient = IsLiveApiConfigured ? new Client(apiKey: _apiKey) : null;

        if (IsLiveApiConfigured)
        {
            _logger.LogInformation("GeminiApiClient configured with live Gemini model: {Model}", _modelName);
        }
        else
        {
            _logger.LogWarning("No Gemini:ApiKey configured. Running in high-fidelity intelligent simulation mode.");
        }
    }

    public async Task<IReadOnlyList<float>?> GenerateEmbeddingAsync(
        string content,
        bool isQuery,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        if (_embeddingClient == null || string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            var config = new EmbedContentConfig
            {
                TaskType = isQuery ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT",
                OutputDimensionality = 768,
                Title = isQuery ? null : title
            };
            var response = await _embeddingClient.Models.EmbedContentAsync(
                _embeddingModel, content, config, cancellationToken);
            return response.Embeddings?.FirstOrDefault()?.Values?.Select(value => (float)value).ToArray();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Search remains available through exact/lexical scoring if embeddings are temporarily unavailable.
            _logger.LogWarning(ex, "Gemini embedding request failed; semantic ranking will fall back to lexical matching.");
            return null;
        }
    }

    public async Task<string> GenerateContentAsync(
        string prompt,
        string? systemInstruction = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsLiveApiConfigured)
        {
            return SimulateTextResponse(prompt, systemInstruction);
        }

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

            var requestBody = new JsonObject
            {
                ["contents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["parts"] = new JsonArray
                        {
                            new JsonObject { ["text"] = prompt }
                        }
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                requestBody["systemInstruction"] = new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject { ["text"] = systemInstruction }
                    }
                };
            }

            return await PostGeminiAsync(url, requestBody, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError("Live Gemini API failed after retries: {Message}", ex.Message);
            if (!_allowSimulationFallback) throw new InvalidOperationException("Gemini is temporarily unavailable. The request was not replaced with simulated content; please retry.", ex);
            _logger.LogWarning("Gemini:AllowSimulationFallback is enabled. Returning simulated text.");
            return SimulateTextResponse(prompt, systemInstruction);
        }
    }

    public async IAsyncEnumerable<string> GenerateContentStreamAsync(
        string prompt,
        string? systemInstruction = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsLiveApiConfigured)
        {
            yield return SimulateTextResponse(prompt, systemInstruction);
            yield break;
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:streamGenerateContent?alt=sse&key={_apiKey}";
        var requestBody = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } }
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            requestBody["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = systemInstruction } }
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            if (detail.Length > 500) detail = detail[..500];
            throw new HttpRequestException($"Gemini streaming returned {(int)response.StatusCode} ({response.StatusCode}). {detail}", null, response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var json = line[5..].Trim();
            if (json.Length == 0 || json == "[DONE]") continue;
            var text = ExtractTextFromGeminiResponse(json);
            if (!string.IsNullOrEmpty(text)) yield return text;
        }
    }

    public async Task<string> GenerateJsonAsync(
        string prompt,
        string? jsonSchema = null,
        string? systemInstruction = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsLiveApiConfigured)
        {
            return SimulateJsonResponse(prompt);
        }

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

            var generationConfig = new JsonObject
            {
                ["responseMimeType"] = "application/json"
            };

            if (!string.IsNullOrWhiteSpace(jsonSchema))
            {
                generationConfig["responseSchema"] = JsonNode.Parse(jsonSchema);
            }

            var requestBody = new JsonObject
            {
                ["contents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["parts"] = new JsonArray
                        {
                            new JsonObject { ["text"] = prompt }
                        }
                    }
                },
                ["generationConfig"] = generationConfig
            };

            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                requestBody["systemInstruction"] = new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject { ["text"] = systemInstruction }
                    }
                };
            }

            return await PostGeminiAsync(url, requestBody, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError("Live Gemini structured JSON API failed after retries: {Message}", ex.Message);
            if (!_allowSimulationFallback) throw new InvalidOperationException("Gemini is temporarily unavailable. The request was not replaced with a simulated proposal; please retry.", ex);
            _logger.LogWarning("Gemini:AllowSimulationFallback is enabled. Returning simulated structured JSON.");
            return SimulateJsonResponse(prompt);
        }
    }

    public async Task<string> GenerateJsonWithFunctionAsync(
        string prompt,
        string? jsonSchema,
        string? systemInstruction,
        string functionName,
        string functionDescription,
        string functionParametersSchema,
        Func<JsonElement, CancellationToken, Task<string>> executeFunction,
        CancellationToken cancellationToken = default)
        => await GenerateJsonWithFunctionsAsync(prompt, jsonSchema, systemInstruction,
            [new GeminiFunctionTool(functionName, functionDescription, functionParametersSchema, executeFunction)], cancellationToken);

    public async Task<string> GenerateJsonWithFunctionsAsync(
        string prompt,
        string? jsonSchema,
        string? systemInstruction,
        IReadOnlyList<GeminiFunctionTool> functions,
        CancellationToken cancellationToken = default)
    {
        if (!IsLiveApiConfigured)
            return await GenerateJsonAsync(prompt, jsonSchema, systemInstruction, cancellationToken);

        if (functions.Count == 0) return await GenerateJsonAsync(prompt, jsonSchema, systemInstruction, cancellationToken);

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
        var contents = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } }
            }
        };
        var requestBody = new JsonObject
        {
            ["contents"] = contents,
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["functionDeclarations"] = new JsonArray(functions.Select(function => (JsonNode)new JsonObject
                    {
                        ["name"] = function.Name,
                        ["description"] = function.Description,
                        ["parameters"] = JsonNode.Parse(function.ParametersSchema)
                    }).ToArray())
                }
            },
            ["toolConfig"] = new JsonObject
            {
                ["functionCallingConfig"] = new JsonObject { ["mode"] = "AUTO" }
            },
            ["generationConfig"] = new JsonObject { ["responseMimeType"] = "application/json" }
        };
        if (!string.IsNullOrWhiteSpace(jsonSchema))
            requestBody["generationConfig"]!["responseSchema"] = JsonNode.Parse(jsonSchema);
        if (!string.IsNullOrWhiteSpace(systemInstruction))
            requestBody["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = systemInstruction } }
            };

        try
        {
            const int maxToolCalls = 8;
            var toolCalls = new List<string>();
            for (var toolTurn = 0; toolTurn <= maxToolCalls; toolTurn++)
            {
                var responseJson = await PostGeminiRawAsync(url, requestBody, cancellationToken);
                using var document = JsonDocument.Parse(responseJson);
                var candidate = document.RootElement.GetProperty("candidates").EnumerateArray().FirstOrDefault();
                if (candidate.ValueKind == JsonValueKind.Undefined)
                    throw new JsonException("Gemini returned no candidate for the tool-enabled chat request.");

                var modelContent = candidate.GetProperty("content");
                var parts = modelContent.GetProperty("parts");
                var functionCall = parts.EnumerateArray()
                    .Select(part => part.TryGetProperty("functionCall", out var call) ? call : default)
                    .FirstOrDefault(call => call.ValueKind == JsonValueKind.Object);
                if (functionCall.ValueKind != JsonValueKind.Object)
                {
                    var text = string.Concat(parts.EnumerateArray()
                        .Where(part => part.TryGetProperty("text", out _))
                        .Select(part => part.GetProperty("text").GetString()));
                    if (string.IsNullOrWhiteSpace(text))
                        throw new JsonException("Gemini returned neither a function call nor a final response.");
                    return text;
                }

                var calledName = functionCall.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                if (toolTurn == maxToolCalls)
                    throw new InvalidOperationException($"Gemini exceeded the maximum of {maxToolCalls} tool calls. Calls: {string.Join(", ", toolCalls)}.");
                var selectedFunction = functions.FirstOrDefault(function =>
                    string.Equals(calledName, function.Name, StringComparison.Ordinal));
                if (selectedFunction is null)
                    throw new InvalidOperationException($"Gemini requested an unsupported function: {calledName ?? "(unnamed)"}.");
                toolCalls.Add(selectedFunction.Name);
                using var emptyArgumentsDocument = JsonDocument.Parse("{}");
                var arguments = functionCall.TryGetProperty("args", out var argsElement)
                    ? argsElement
                    : emptyArgumentsDocument.RootElement;
                var functionResult = await selectedFunction.Execute(arguments, cancellationToken);
                var resultNode = JsonNode.Parse(functionResult) ?? new JsonObject();
                var functionResponse = new JsonObject
                {
                    ["name"] = selectedFunction.Name,
                    ["response"] = resultNode
                };
                if (functionCall.TryGetProperty("id", out var callIdElement) && callIdElement.ValueKind == JsonValueKind.String)
                    functionResponse["id"] = callIdElement.GetString();

                contents.Add(JsonNode.Parse(modelContent.GetRawText()));
                contents.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["functionResponse"] = functionResponse
                        }
                    }
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError("Live Gemini function-calling request failed: {Message}", ex.Message);
            if (!_allowSimulationFallback)
                throw new InvalidOperationException("Gemini is temporarily unavailable. The request was not replaced with simulated content; please retry.", ex);
            _logger.LogWarning("Gemini:AllowSimulationFallback is enabled. Falling back to structured JSON generation without function calls.");
            return await GenerateJsonAsync(prompt, jsonSchema, systemInstruction, cancellationToken);
        }

        throw new InvalidOperationException("Gemini function-calling request did not complete.");
    }

    public async Task<string> AnalyzeImageAsync(
        byte[] imageBytes,
        string mimeType,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (!IsLiveApiConfigured)
        {
            return SimulateVisionResponse(prompt);
        }

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
            var base64Data = Convert.ToBase64String(imageBytes);

            var requestBody = new JsonObject
            {
                ["contents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["parts"] = new JsonArray
                        {
                            new JsonObject { ["text"] = prompt },
                            new JsonObject
                            {
                                ["inlineData"] = new JsonObject
                                {
                                    ["mimeType"] = mimeType,
                                    ["data"] = base64Data
                                }
                            }
                        }
                    }
                },
                ["generationConfig"] = new JsonObject
                {
                    ["responseMimeType"] = "application/json"
                }
            };

            return await PostGeminiAsync(url, requestBody, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError("Live Gemini Vision API failed after retries: {Message}", ex.Message);
            if (!_allowSimulationFallback) throw new InvalidOperationException("Gemini Vision is temporarily unavailable. The request was not replaced with simulated analysis; please retry.", ex);
            _logger.LogWarning("Gemini:AllowSimulationFallback is enabled. Returning simulated vision output.");
            return SimulateVisionResponse(prompt);
        }
    }

    private async Task<string> PostGeminiAsync(string url, JsonObject requestBody, CancellationToken cancellationToken)
    {
        return ExtractTextFromGeminiResponse(await PostGeminiRawAsync(url, requestBody, cancellationToken));
    }

    private async Task<string> PostGeminiRawAsync(string url, JsonObject requestBody, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode) return responseJson;

            var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (!transient || attempt == maxAttempts)
            {
                var detail = responseJson.Length > 500 ? responseJson[..500] : responseJson;
                throw new HttpRequestException($"Gemini returned {(int)response.StatusCode} ({response.StatusCode}). {detail}", null, response.StatusCode);
            }

            var retryAfter = response.Headers.RetryAfter?.Delta;
            var delay = retryAfter ?? TimeSpan.FromMilliseconds(attempt == 1 ? 500 : 1_500);
            _logger.LogWarning("Gemini returned {StatusCode}; retrying attempt {NextAttempt}/{MaxAttempts} after {DelayMs}ms.",
                (int)response.StatusCode, attempt + 1, maxAttempts, delay.TotalMilliseconds);
            await Task.Delay(delay, cancellationToken);
        }
        throw new InvalidOperationException("Gemini request did not complete.");
    }

    private static string ExtractTextFromGeminiResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() > 0)
        {
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            if (parts.GetArrayLength() > 0)
            {
                return parts[0].GetProperty("text").GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static string SimulateTextResponse(string prompt, string? systemInstruction)
    {
        if (prompt.Contains("route", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("landmark", StringComparison.OrdinalIgnoreCase))
        {
            return "From your current location, head straight down the main walkway. You will pass Starbucks on your left. Continue towards the Central Atrium and take the central escalator up to Level 2. Once on Level 2, turn right into the North Corridor. Your destination will be approximately 15 meters ahead on your left.";
        }

        if (prompt.Contains("insight", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("business intelligence", StringComparison.OrdinalIgnoreCase))
        {
            return "Family and children discovery requests surged by 31% this past weekend, predominantly occurring between 1:00 PM and 5:00 PM. Meanwhile, 3 active tenant campaigns (including Popular Bookstore's Reading Week and Toys'R'Us LEGO specials) directly address this demand. Mall management should consider featuring these campaigns prominently on Ground Floor digital totems and weekend promotional pushes.";
        }

        return "Based on verified mall information, here are recommendations tailored to your query. Feel free to ask for directions or filter by budget and category.";
    }

    private static string SimulateJsonResponse(string prompt)
    {
        if (prompt.Contains("proposed_markdown", StringComparison.OrdinalIgnoreCase))
        {
            var marker = "Current canonical Markdown:";
            var current = prompt.Contains(marker, StringComparison.Ordinal) ? prompt[(prompt.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..] : "";
            current = current.Split("---", StringSplitOptions.None).Skip(1).FirstOrDefault()?.Trim() ?? current.Trim();
            var instruction = ExtractQuotedPromptValue(prompt, "Instruction:");
            var normalizedInstruction = instruction.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
            var editWords = new[] { "improve", "enrich", "add", "remove", "rewrite", "fix", "correct", "shorten", "expand", "update", "restructure", "change" };
            var asksForEdit = editWords.Any(word => instruction.Contains(word, StringComparison.OrdinalIgnoreCase));
            var conversational = normalizedInstruction is "hi" or "hello" or "hey" or "thanks" or "thank you" || (!asksForEdit && instruction.TrimEnd().EndsWith('?'));
            if (conversational)
            {
                return JsonSerializer.Serialize(new
                {
                    action = "reply",
                    proposed_markdown = current,
                    user_message = normalizedInstruction is "hi" or "hello" or "hey"
                        ? "Hello! I can help improve this knowledge or answer questions about its current content."
                        : "I reviewed the current content and answered without changing the document.",
                    reasoning_summary = "No document change was needed for this conversational request.",
                    changes = Array.Empty<string>()
                });
            }
            return JsonSerializer.Serialize(new
            {
                action = "propose",
                proposed_markdown = $"{current}\n\n## AI-proposed enrichment\n\n- Review the supplied evidence and confirm this addition before publication.",
                user_message = "I added a clearly separated section so the new information is easier to review before publishing.",
                reasoning_summary = "The existing content did not clearly separate the additional information. A dedicated section makes it easier for an operator to verify before it becomes shopper-facing.",
                changes = new[] { "Added a proposed enrichment section based on the instruction and evidence." }
            });
        }

        if (prompt.Contains("knowledge investigator", StringComparison.OrdinalIgnoreCase))
        {
            var clarification = prompt.Contains("New clarification: none", StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<string>()
                : new[] { "The latest conversational answer should be reviewed before public publication." };
            return JsonSerializer.Serialize(new
            {
                summary = "The imported material has been retained as searchable knowledge. This simulated analysis highlights its main content without inventing missing operational facts.",
                claims = new[] { "The source contains information supplied by a mall or tenant contributor." },
                questions = clarification.Length == 0 ? new[] { "Is there any missing validity, location, or restriction information that shoppers should know?" } : clarification
            });
        }

        // Shopper intent extraction prompt
        if (prompt.Contains("intent", StringComparison.OrdinalIgnoreCase))
        {
            // Inspect only the shopper's message. The surrounding prompt contains examples such as
            // "8-year-old nephew" and must never be mistaken for user input.
            var message = ExtractQuotedPromptValue(prompt, "Current message:");
            bool isNephew = message.Contains("nephew", StringComparison.OrdinalIgnoreCase) || message.Contains("kid", StringComparison.OrdinalIgnoreCase) || message.Contains("child", StringComparison.OrdinalIgnoreCase);
            bool isMother = message.Contains("mum", StringComparison.OrdinalIgnoreCase) || message.Contains("mother", StringComparison.OrdinalIgnoreCase);
            bool isDinner = message.Contains("hungry", StringComparison.OrdinalIgnoreCase) || message.Contains("dinner", StringComparison.OrdinalIgnoreCase) || message.Contains("eat", StringComparison.OrdinalIgnoreCase) || message.Contains("food", StringComparison.OrdinalIgnoreCase) || message.Contains("restaurant", StringComparison.OrdinalIgnoreCase);

            if (isNephew)
            {
                return JsonSerializer.Serialize(new
                {
                    intent = "gift",
                    recipient = "nephew aged 8",
                    category_interests = new[] { "toys", "books", "games" },
                    excluded_categories = new[] { "fashion", "clothes" },
                    preferences = new[] { "reading", "educational", "building" },
                    budget_signal = "mid"
                });
            }

            if (isMother)
            {
                return JsonSerializer.Serialize(new
                {
                    intent = "gift",
                    recipient = "mother",
                    category_interests = new[] { "food", "kitchen", "beauty" },
                    excluded_categories = new[] { "fashion", "clothes" },
                    preferences = prompt.Contains("cooking", StringComparison.OrdinalIgnoreCase) ? new[] { "cooking" } : Array.Empty<string>(),
                    budget_signal = "mid"
                });
            }

            if (isDinner)
            {
                return JsonSerializer.Serialize(new
                {
                    intent = "dining",
                    recipient = "parents",
                    category_interests = new[] { "chinese", "dumplings", "hotpot" },
                    excluded_categories = Array.Empty<string>(),
                    preferences = new[] { "near cinema", "sit-down" },
                    budget_signal = "mid"
                });
            }

            return JsonSerializer.Serialize(new
            {
                intent = "general_discovery",
                recipient = (string?)null,
                category_interests = new[] { "shopping" },
                excluded_categories = Array.Empty<string>(),
                preferences = Array.Empty<string>(),
                budget_signal = "mid"
            });
        }

        return "{}";
    }

    private static string ExtractQuotedPromptValue(string prompt, string label)
    {
        var labelIndex = prompt.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (labelIndex < 0) return prompt;
        var valueStart = labelIndex + label.Length;
        var lineEnd = prompt.IndexOf('\n', valueStart);
        var value = lineEnd < 0 ? prompt[valueStart..] : prompt[valueStart..lineEnd];
        return value.Trim().Trim('"');
    }

    private static string SimulateVisionResponse(string prompt)
    {
        if (prompt.Contains("floor plan", StringComparison.OrdinalIgnoreCase) || prompt.Contains("routing graph", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                nodes = new object[]
                {
                    new { key = "entrance", label = "Detected Main Entrance", type = "Entrance", x = 8, y = 50, description = "Draft entrance detected from the imported plan", confidence = 0.82 },
                    new { key = "junction", label = "Central Corridor Junction", type = "Junction", x = 50, y = 50, description = "Draft public corridor junction", confidence = 0.76 },
                    new { key = "destination", label = "Detected Destination Area", type = "Landmark", x = 88, y = 50, description = "Label requires operator confirmation", confidence = 0.64 }
                },
                edges = new object[]
                {
                    new { from = "entrance", to = "junction", distance_meters = 35, accessible = true, bidirectional = true, direction_hint = "walk_straight" },
                    new { from = "junction", to = "destination", distance_meters = 32, accessible = true, bidirectional = true, direction_hint = "walk_straight" }
                },
                warnings = new[] { "Simulation mode cannot verify walls, restricted areas, or vertical connectors. Review before publishing." },
                confidence_percentage = 68
            });
        }

        if (prompt.Contains("knowledge investigator", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                summary = "The uploaded visual source was retained and examined as general knowledge.",
                claims = new[] { "The source contains visual information requiring contributor review." },
                questions = new[] { "Which details from this source should be made visible in public search?" }
            });
        }

        // Poster extraction prompt
        if (prompt.Contains("poster", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("promotion", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                title = "Mid-Autumn Special Mooncake Festival",
                promotion_type = "Discount",
                offer = "Buy 2 Mooncake Sets Enjoy 15% Off",
                valid_from = DateTime.UtcNow.ToString("yyyy-MM-01"),
                valid_to = DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-30"),
                categories = new[] { "Food & Beverage", "Gifting", "Festive", "Mooncake" },
                target_audience = "Family, Corporate Gifting",
                occasions = "Mid-Autumn Festival, Visiting Relatives",
                missing_information = new[]
                {
                    "Is advance ordering required for corporate or bulk purchases?",
                    "Are there dietary specifications (e.g., Halal or Vegetarian)?"
                }
            });
        }

        // Multimodal location detection from photo
        return JsonSerializer.Serialize(new
        {
            detected_landmarks = new[] { "Guardian Pharmacy", "Watsons", "Starbucks", "Level 2 Signage" },
            estimated_location = "Level 2, South Corridor Junction",
            estimated_floor = 2,
            confidence_percentage = 94,
            confirmation_question = "I think you're near Guardian Pharmacy on Level 2. Is Starbucks across from you?"
        });
    }
}
