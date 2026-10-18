using MallSense.Domain.Common;

namespace MallSense.Domain.Entities;

public enum KnowledgeVisibility { Private = 0, Published = 1 }
public enum KnowledgeStatus { Draft = 0, NeedsClarification = 1, PendingApproval = 2, Published = 3, Failed = 4 }
public enum AgentActionMode { Explore = 0, Suggest = 1, AskFirst = 2, AutoWithinScope = 3 }
public enum KnowledgeSuggestionStatus { Pending = 0, Accepted = 1, Rejected = 2 }

/// <summary>Internal ownership scope for mall-wide or tenant knowledge.</summary>
public class KnowledgeCollection : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "Global";
    public Guid? ScopeReferenceId { get; set; }
    public KnowledgeVisibility Visibility { get; set; }
    public virtual ICollection<KnowledgeSource> Sources { get; set; } = new List<KnowledgeSource>();
}

/// <summary>The original imported material plus AI investigation output. Content can remain unstructured.</summary>
public class KnowledgeSource : BaseEntity
{
    public Guid CollectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourceKind { get; set; } = "Text";
    public string MimeType { get; set; } = "text/plain";
    public string? StorageReference { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AiSummary { get; set; } = string.Empty;
    public string SuggestedQuestionsJson { get; set; } = "[]";
    public string ClaimsJson { get; set; } = "[]";
    public KnowledgeStatus Status { get; set; } = KnowledgeStatus.Draft;
    public bool IsSimulated { get; set; }
    public string? ModelName { get; set; }
    public string PromptVersion { get; set; } = "knowledge-investigator-v1";
    public virtual KnowledgeCollection Collection { get; set; } = null!;
    public virtual ICollection<KnowledgeConversationTurn> Conversation { get; set; } = new List<KnowledgeConversationTurn>();
    public virtual ICollection<KnowledgeSuggestion> Suggestions { get; set; } = new List<KnowledgeSuggestion>();
    public virtual ICollection<KnowledgeSourceAttachment> Attachments { get; set; } = new List<KnowledgeSourceAttachment>();
}

/// <summary>An AI-authored Markdown proposal. It never changes canonical source content until explicitly accepted.</summary>
public class KnowledgeSuggestion : BaseEntity
{
    public Guid SourceId { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public string BaseMarkdown { get; set; } = string.Empty;
    public string ProposedMarkdown { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string ChangesJson { get; set; } = "[]";
    public string EvidenceNamesJson { get; set; } = "[]";
    public KnowledgeSuggestionStatus Status { get; set; } = KnowledgeSuggestionStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public virtual KnowledgeSource Source { get; set; } = null!;
}

public class KnowledgeSourceAttachment : BaseEntity
{
    public Guid SourceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public string Content { get; set; } = string.Empty;
    public virtual KnowledgeSource Source { get; set; } = null!;
}

public class KnowledgeConversationTurn : BaseEntity
{
    public Guid SourceId { get; set; }
    public int Sequence { get; set; }
    public string Role { get; set; } = "assistant";
    public string Message { get; set; } = string.Empty;
    public virtual KnowledgeSource Source { get; set; } = null!;
}

public class FloorPlanDraft : BaseEntity
{
    public Guid SourceId { get; set; }
    public int FloorNumber { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public string NodesJson { get; set; } = "[]";
    public string EdgesJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
    public int ConfidencePercentage { get; set; }
    public KnowledgeStatus Status { get; set; } = KnowledgeStatus.Draft;
    public virtual KnowledgeSource Source { get; set; } = null!;
}
