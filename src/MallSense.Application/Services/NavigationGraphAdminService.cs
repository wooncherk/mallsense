using System.Text.Json;
using MallSense.Application.DTOs;
using MallSense.Application.Interfaces;
using MallSense.Domain.Entities;
using MallSense.Domain.Interfaces;

namespace MallSense.Application.Services;

public class NavigationGraphAdminService : INavigationGraphAdminService
{
    private readonly INavigationGraphRepository _repository;
    private readonly IGeminiClient _geminiClient;
    private readonly ITenantRepository _tenantRepository;

    public NavigationGraphAdminService(
        INavigationGraphRepository repository,
        IGeminiClient geminiClient,
        ITenantRepository tenantRepository)
    {
        _repository = repository;
        _geminiClient = geminiClient;
        _tenantRepository = tenantRepository;
    }

    public async Task<NavigationNode> CreateNodeAsync(SaveNavigationNodeRequest request, CancellationToken cancellationToken = default)
    {
        ValidateNode(request);
        await ValidateTenantAssignmentAsync(request.TenantId, null, cancellationToken);
        return await _repository.AddNodeAsync(ToNode(new NavigationNode(), request), cancellationToken);
    }

    public async Task<NavigationNode> UpdateNodeAsync(Guid id, SaveNavigationNodeRequest request, CancellationToken cancellationToken = default)
    {
        ValidateNode(request);
        var node = await _repository.GetNodeByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Navigation node not found.");
        await ValidateTenantAssignmentAsync(request.TenantId, id, cancellationToken);
        return await _repository.UpdateNodeAsync(ToNode(node, request), cancellationToken);
    }

    public async Task DeleteNodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.GetNodeByIdAsync(id, cancellationToken) == null) throw new KeyNotFoundException("Navigation node not found.");
        var edges = await _repository.GetAllEdgesAsync(cancellationToken);
        if (edges.Any(e => e.SourceNodeId == id || e.TargetNodeId == id))
            throw new InvalidOperationException("Remove this node's connections before deleting it.");
        await _repository.DeleteNodeAsync(id, cancellationToken);
    }

    public async Task<NavigationEdge> CreateEdgeAsync(SaveNavigationEdgeRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateEdgeAsync(request, null, cancellationToken);
        return await _repository.AddEdgeAsync(ToEdge(new NavigationEdge(), request), cancellationToken);
    }

    public async Task<NavigationEdge> UpdateEdgeAsync(Guid id, SaveNavigationEdgeRequest request, CancellationToken cancellationToken = default)
    {
        var edge = await _repository.GetEdgeByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Navigation connection not found.");
        await ValidateEdgeAsync(request, id, cancellationToken);
        return await _repository.UpdateEdgeAsync(ToEdge(edge, request), cancellationToken);
    }

    public async Task DeleteEdgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.GetEdgeByIdAsync(id, cancellationToken) == null) throw new KeyNotFoundException("Navigation connection not found.");
        await _repository.DeleteEdgeAsync(id, cancellationToken);
    }

    public async Task<NavigationGraphValidationDto> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _repository.GetAllNodesAsync(cancellationToken);
        var edges = await _repository.GetAllEdgesAsync(cancellationToken);
        var ids = nodes.Select(n => n.Id).ToHashSet();
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (var edge in edges)
        {
            if (!ids.Contains(edge.SourceNodeId) || !ids.Contains(edge.TargetNodeId)) errors.Add($"Connection {edge.Id} refers to a missing node.");
            if (edge.SourceNodeId == edge.TargetNodeId) errors.Add($"Connection {edge.Id} connects a node to itself.");
        }
        var connected = edges.SelectMany(e => new[] { e.SourceNodeId, e.TargetNodeId }).ToHashSet();
        foreach (var node in nodes.Where(n => !connected.Contains(n.Id))) warnings.Add($"{node.Label} on Level {node.FloorNumber} is isolated.");
        foreach (var group in edges.GroupBy(e => e.SourceNodeId.CompareTo(e.TargetNodeId) < 0 ? (e.SourceNodeId, e.TargetNodeId) : (e.TargetNodeId, e.SourceNodeId)).Where(g => g.Count() > 1))
            warnings.Add($"Duplicate connection between {group.Key.Item1} and {group.Key.Item2}.");
        return new NavigationGraphValidationDto(errors.Count == 0, errors, warnings);
    }

    public async Task<NavigationAiDraftDto> GenerateAiDraftAsync(
        NavigationAiDraftRequest request,
        IReadOnlyList<NavigationAiEvidenceDto> evidence,
        CancellationToken cancellationToken = default)
    {
        if (evidence.Count == 0 && string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Upload a floor plan or describe what the AI should add.");

        var currentDraft = JsonSerializer.Serialize(new { nodes = request.CurrentNodes ?? [], edges = request.CurrentEdges ?? [] });
        var tenantDirectory = JsonSerializer.Serialize((await _tenantRepository.GetAllAsync(cancellationToken))
            .Where(tenant => tenant.IsActive)
            .OrderBy(tenant => tenant.FloorNumber)
            .ThenBy(tenant => tenant.UnitNumber)
            .Select(tenant => new
            {
                tenantId = tenant.Id,
                tenant.Name,
                tenant.FloorNumber,
                tenant.UnitNumber
            }));
        var conversation = string.Join("\n", (request.Conversation ?? []).TakeLast(12).Select(x => $"{x.Role}: {x.Message}"));
        var prompt = $$"""
            You are an indoor navigation graph assistant working with a human mall operator in multiple turns.
            Study floor plans, sketches, directory screenshots, photos, handwritten clues, or text descriptions conservatively.
            Refine the current draft rather than starting over. Produce the best conservative proposal in one pass. Do not
            require conversational confirmation; record uncertainty in warnings and omit unsupported connections.
            Nodes may use any human label, but type must be one of Shop, Junction, Escalator, Lift, Entrance, Restroom, Surau, InformationCounter, Landmark.
            Coordinates are normalized 0-100. When a floor-plan image is present, derive every node position directly from
            that image, where its top-left is (0,0) and bottom-right is (100,100). Only add an edge when a walkable connection is supported. The operator will
            compare the proposal with the current map and explicitly apply it in the interface.
            Incorporate every uploaded floor-plan image in the current proposal; never defer an uploaded floor to a later
            pass. Use clear filename and drawing labels such as GF, LG, 1F, 2F, or 3F as floor evidence. If an uploaded plan
            represents a floor that is absent from the current graph, add that floor's supported locations and routes now.
            Place each node at the center of the feature it represents and preserve the real relative layout from the floor
            plan. Do not stack multiple nodes at the same or nearly the same coordinates. Keep labels readable by separating
            nearby nodes slightly without changing their real ordering or corridor side. When one existing label combines
            distinct facilities (for example "Lift & Stairs"), split it into separate Lift and Stairs nodes only when the
            evidence shows distinct positions or distinct routes; otherwise preserve the combined location rather than guess.
            Work on the complete multi-floor mall graph. Determine each location's floor from the evidence, existing graph,
            and authoritative tenant directory; do not privilege or focus on any one floor. Every node must include its
            floor_number. Preserve every supported location already present in the current draft. Treat graph completeness
            as a required final check: every returned node must have at least one supported walkable edge, and all returned
            nodes on each floor must form one connected routing component unless the evidence explicitly proves an area is
            inaccessible. Connect matching lift, escalator, or stair endpoints across floors only when supported by the
            evidence or current graph. Cross-floor edges must reference the endpoint keys on both floors.
            In particular, repair isolated lifts, stairs, entrances, shops, and landmarks by linking them to the nearest
            evidenced corridor or junction. Never remove or rename a location merely to make validation pass. For a shop or
            other tenant destination, compare any visible unit number with the authoritative tenant directory below. When a
            unit number matches, use that tenant's exact directory name as the node label. Do not invent a tenant or unit
            number, and record ambiguous or unreadable unit numbers in warnings. The application will automatically map
            exact tenant-name matches when the proposal is applied.
            Before returning JSON, check the proposal yourself for missing nodes, dangling edge references, self-links,
            duplicate links, isolated nodes, disconnected routing islands, and omitted existing locations. Put any issue you
            cannot safely resolve in warnings and explain what evidence is missing. This check is repeated after apply, and
            its findings may be supplied in a later turn for repair.
            Latest operator message: {{request.Message ?? "No additional text clue."}}
            Conversation:
            {{conversation}}
            Current draft:
            {{currentDraft}}
            Authoritative active tenant directory for all floors (tenantId, name, floorNumber, unitNumber):
            {{tenantDirectory}}
            Return JSON with assistant_message, clarification_questions, nodes, edges, warnings, confidence_percentage.
            The assistant_message must summarize the whole-mall proposal and the floors you selected; never frame the result
            as an update to one active floor. Each node: key,floor_number,label,type,x,y,description,confidence. Node keys must be unique across the entire mall.
            Each edge: from,to,distance_meters,accessible,bidirectional,direction_hint, where from and to reference node keys.
            """;

        var analyses = new List<string>();
        foreach (var item in evidence.Take(8))
            analyses.Add(await _geminiClient.AnalyzeImageAsync(item.Bytes, item.MimeType, $"{prompt}\nEvidence file: {item.FileName}", cancellationToken));

        string json;
        if (analyses.Count == 1)
        {
            json = analyses[0];
        }
        else
        {
            var evidenceText = analyses.Count == 0 ? "No visual evidence." : string.Join("\n--- EVIDENCE ANALYSIS ---\n", analyses);
            json = await _geminiClient.GenerateJsonAsync($"{prompt}\nReconcile these evidence analyses into one draft:\n{evidenceText}", AiDraftSchema, cancellationToken: cancellationToken);
        }

        return ParseAiDraft(json);
    }

    private static NavigationAiDraftDto ParseAiDraft(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var warnings = ReadStrings(root, "warnings");
        var nodes = new List<NavigationAiDraftNodeDto>();
        if (TryGetProperty(root, "nodes", out var nodeArray) && nodeArray.ValueKind == JsonValueKind.Array)
            foreach (var x in nodeArray.EnumerateArray())
            {
                var key = GetString(x, "key", "");
                var floorNumber = GetInt(x, "floor_number", int.MinValue);
                var label = GetString(x, "label", "");
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(label) || floorNumber is < -10 or > 200)
                {
                    warnings.Add("Ignored an AI location that was missing a valid key, label, or floor number.");
                    continue;
                }
                nodes.Add(new(key, floorNumber, label, GetString(x, "type", "Landmark"),
                    GetDouble(x, "x", 50), GetDouble(x, "y", 50),
                    TryGetProperty(x, "description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null,
                    GetDouble(x, "confidence", .5)));
            }
        var edges = new List<NavigationAiDraftEdgeDto>();
        var validKeys = nodes.Select(node => node.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (TryGetProperty(root, "edges", out var edgeArray) && edgeArray.ValueKind == JsonValueKind.Array)
            foreach (var x in edgeArray.EnumerateArray())
            {
                var from = GetString(x, "from", "");
                var to = GetString(x, "to", "");
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || from == to || !validKeys.Contains(from) || !validKeys.Contains(to))
                {
                    warnings.Add("Ignored an AI connection with missing, identical, or unknown endpoint keys.");
                    continue;
                }
                edges.Add(new(from, to, GetDouble(x, "distance_meters", 10),
                    !TryGetProperty(x, "accessible", out var a) || a.ValueKind is not JsonValueKind.False,
                    !TryGetProperty(x, "bidirectional", out var b) || b.ValueKind is not JsonValueKind.False,
                    GetString(x, "direction_hint", "walk_straight")));
            }
        warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var questions = ReadStrings(root, "clarification_questions");
        var assistant = GetString(root, "assistant_message", nodes.Count > 0
            ? $"I prepared a draft with {nodes.Count} locations and {edges.Count} walkable connections. Review it or tell me what to correct."
            : "I need a little more information before I can propose reliable navigation nodes.");
        if (questions.Count == 0 && nodes.Count == 0) questions.Add("Which entrances, destinations and corridor connections should be represented?");
        return new NavigationAiDraftDto(assistant, questions, nodes, edges, warnings, (int)GetDouble(root, "confidence_percentage", 50));
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var normalizedName = NormalizePropertyName(name);
        foreach (var property in element.EnumerateObject())
            if (NormalizePropertyName(property.Name) == normalizedName)
            {
                value = property.Value;
                return true;
            }
        value = default;
        return false;
    }

    private static string NormalizePropertyName(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string GetString(JsonElement element, string name, string fallback) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
    private static double GetDouble(JsonElement element, string name, double fallback) =>
        TryGetProperty(element, name, out var value) && value.TryGetDouble(out var result) ? result : fallback;
    private static int GetInt(JsonElement element, string name, int fallback) =>
        TryGetProperty(element, name, out var value) && value.TryGetInt32(out var result) ? result : fallback;
    private static List<string> ReadStrings(JsonElement root, string name) =>
        TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList()
            : [];

    private const string AiDraftSchema = """{"type":"object","properties":{"assistant_message":{"type":"string"},"clarification_questions":{"type":"array","items":{"type":"string"}},"nodes":{"type":"array","items":{"type":"object","properties":{"key":{"type":"string"},"floor_number":{"type":"integer"},"label":{"type":"string"},"type":{"type":"string"},"x":{"type":"number"},"y":{"type":"number"},"description":{"type":"string"},"confidence":{"type":"number"}},"required":["key","floor_number","label","type","x","y"]}},"edges":{"type":"array","items":{"type":"object","properties":{"from":{"type":"string"},"to":{"type":"string"},"distance_meters":{"type":"number"},"accessible":{"type":"boolean"},"bidirectional":{"type":"boolean"},"direction_hint":{"type":"string"}},"required":["from","to"]}},"warnings":{"type":"array","items":{"type":"string"}},"confidence_percentage":{"type":"integer"}},"required":["assistant_message","clarification_questions","nodes","edges","warnings","confidence_percentage"]}""";

    private async Task ValidateEdgeAsync(SaveNavigationEdgeRequest request, Guid? existingId, CancellationToken cancellationToken)
    {
        if (request.SourceNodeId == request.TargetNodeId) throw new ArgumentException("A connection requires two different nodes.");
        if (request.DistanceMeters <= 0 || double.IsNaN(request.DistanceMeters) || double.IsInfinity(request.DistanceMeters)) throw new ArgumentException("Distance must be greater than zero.");
        var source = await _repository.GetNodeByIdAsync(request.SourceNodeId, cancellationToken) ?? throw new ArgumentException("Source node not found.");
        var target = await _repository.GetNodeByIdAsync(request.TargetNodeId, cancellationToken) ?? throw new ArgumentException("Target node not found.");
        if (target.FloorNumber - source.FloorNumber != request.FloorChange) throw new ArgumentException("Floor change must match the node floors.");
        var edges = await _repository.GetAllEdgesAsync(cancellationToken);
        if (edges.Any(e => e.Id != existingId && ((e.SourceNodeId == request.SourceNodeId && e.TargetNodeId == request.TargetNodeId) || (e.IsBidirectional && e.SourceNodeId == request.TargetNodeId && e.TargetNodeId == request.SourceNodeId))))
            throw new ArgumentException("These nodes are already connected.");
    }

    private async Task ValidateTenantAssignmentAsync(Guid? tenantId, Guid? nodeId, CancellationToken cancellationToken)
    {
        if (!tenantId.HasValue) return;
        var nodes = await _repository.GetAllNodesAsync(cancellationToken);
        if (nodes.Any(n => n.TenantId == tenantId && n.Id != nodeId))
            throw new ArgumentException("This tenant is already linked to another navigation node.");
    }

    private static void ValidateNode(SaveNavigationNodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label)) throw new ArgumentException("Node label is required.");
        if (request.FloorNumber < -10 || request.FloorNumber > 200) throw new ArgumentException("Floor number is outside the supported range.");
        if (!double.IsFinite(request.CoordinateX) || !double.IsFinite(request.CoordinateY)) throw new ArgumentException("Coordinates must be valid numbers.");
    }

    private static NavigationNode ToNode(NavigationNode node, SaveNavigationNodeRequest r)
    {
        node.FloorNumber = r.FloorNumber; node.Label = r.Label.Trim(); node.NodeType = r.NodeType;
        node.CoordinateX = r.CoordinateX; node.CoordinateY = r.CoordinateY; node.Description = r.Description?.Trim(); node.TenantId = r.TenantId;
        return node;
    }

    private static NavigationEdge ToEdge(NavigationEdge edge, SaveNavigationEdgeRequest r)
    {
        edge.SourceNodeId = r.SourceNodeId; edge.TargetNodeId = r.TargetNodeId; edge.DistanceMeters = r.DistanceMeters;
        edge.DirectionHint = string.IsNullOrWhiteSpace(r.DirectionHint) ? "walk_straight" : r.DirectionHint.Trim();
        edge.FloorChange = r.FloorChange; edge.IsAccessible = r.IsAccessible; edge.IsBidirectional = r.IsBidirectional;
        return edge;
    }
}
