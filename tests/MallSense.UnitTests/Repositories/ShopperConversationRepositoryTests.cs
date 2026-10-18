using MallSense.Domain.Entities;
using MallSense.Infrastructure.Data;
using MallSense.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MallSense.UnitTests.Repositories;

public class ShopperConversationRepositoryTests
{
    [Fact]
    public async Task AppendExchangeAsync_PersistsLatestConversationForOnlyItsGuest()
    {
        var options = new DbContextOptionsBuilder<MallDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new MallDbContext(options);
        var guest = new ApplicationUser { UserName = "guest_history", FullName = "Mall Visitor", Role = UserRole.Guest };
        var otherGuest = new ApplicationUser { UserName = "guest_other", FullName = "Mall Visitor", Role = UserRole.Guest };
        context.Users.AddRange(guest, otherGuest);
        await context.SaveChangesAsync();
        var repository = new ShopperConversationRepository(context);

        const string suggestions = "[{\"label\":\"Chinese\",\"prompt\":\"We prefer Chinese cuisine.\"}]";
        await repository.AppendExchangeAsync(guest.Id, "session-1", "AskAI", "Where can I eat?", "Try the food court.", null, "[]", suggestions);
        await repository.AppendExchangeAsync(guest.Id, "session-1", "AskAI", "Any halal options?", "Yes, here are some options.", null, "[]", "[]");

        var latest = await repository.GetLatestAsync(guest.Id);
        Assert.NotNull(latest);
        Assert.Equal("session-1", latest.SessionId);
        Assert.Equal(new[] { "user", "assistant", "user", "assistant" }, latest.Turns.OrderBy(turn => turn.Sequence).Select(turn => turn.Role));
        Assert.Equal(suggestions, latest.Turns.OrderBy(turn => turn.Sequence).First(turn => turn.Role == "assistant").ReplySuggestionsJson);
        Assert.Null(await repository.GetLatestAsync(otherGuest.Id));
    }

    [Fact]
    public async Task ConversationManagement_ListsLoadsAndDeletesOnlyOwnedSessions()
    {
        var options = new DbContextOptionsBuilder<MallDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new MallDbContext(options);
        var guest = new ApplicationUser { UserName = "guest_manager", FullName = "Mall Visitor", Role = UserRole.Guest };
        var otherGuest = new ApplicationUser { UserName = "guest_manager_other", FullName = "Mall Visitor", Role = UserRole.Guest };
        context.Users.AddRange(guest, otherGuest);
        await context.SaveChangesAsync();
        var repository = new ShopperConversationRepository(context);

        await repository.AppendExchangeAsync(guest.Id, "owned-1", "AskAI", "Find dinner", "Here are some options.", null, "[]", "[]");
        await repository.AppendExchangeAsync(guest.Id, "owned-2", "AskAI", "Find shoes", "Try these stores.", null, "[]", "[]");
        await repository.AppendExchangeAsync(otherGuest.Id, "other-1", "AskAI", "Find coffee", "Try the cafe.", null, "[]", "[]");

        var conversations = await repository.GetAllAsync(guest.Id);
        Assert.Equal(2, conversations.Count);
        Assert.NotNull(await repository.GetBySessionIdAsync(guest.Id, "owned-1"));
        Assert.Null(await repository.GetBySessionIdAsync(guest.Id, "other-1"));
        Assert.False(await repository.DeleteAsync(guest.Id, "other-1"));
        Assert.True(await repository.DeleteAsync(guest.Id, "owned-1"));
        Assert.Null(await repository.GetBySessionIdAsync(guest.Id, "owned-1"));
    }
}
