using MallSense.Domain.Common;

namespace MallSense.Domain.Entities;

public class ShopperConversation : BaseEntity
{
    public Guid UserId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string SurfaceContext { get; set; } = "AskAI";
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<ShopperConversationTurn> Turns { get; set; } = new List<ShopperConversationTurn>();
}

public class ShopperConversationTurn : BaseEntity
{
    public Guid ConversationId { get; set; }
    public int Sequence { get; set; }
    public string Role { get; set; } = string.Empty;
    public string MessageType { get; set; } = "Message";
    public string Message { get; set; } = string.Empty;
    public string? StructuredIntentJson { get; set; }
    public string? RecommendationsJson { get; set; }
    public string? ReplySuggestionsJson { get; set; }
    public virtual ShopperConversation Conversation { get; set; } = null!;
}
