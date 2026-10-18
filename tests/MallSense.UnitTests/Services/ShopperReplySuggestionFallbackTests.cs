using MallSense.Application.DTOs;
using MallSense.Application.Services;

namespace MallSense.UnitTests.Services;

public class ShopperReplySuggestionFallbackTests
{
    [Fact]
    public void Build_UsesMealFormatChoices_WhenAssistantAsksHowTheShopperWantsToEat()
    {
        var request = CreateRequest("What food deals are available?");

        var suggestions = ShopperReplySuggestionFallback.Build(
            request,
            "Are you looking for a quick bite, a cafe beverage, or a sit-down meal?");

        Assert.Equal(["Quick bite", "Cafe beverage", "Sit-down meal"], suggestions.Select(item => item.Label));
    }

    [Fact]
    public void Build_UsesDealCategories_WhenTheConversationIsAboutOffers()
    {
        var request = CreateRequest("What promotions are available today?");

        var suggestions = ShopperReplySuggestionFallback.Build(
            request,
            "There are several current offers available around the mall.");

        Assert.Equal(["Dining deals", "Shopping deals", "Mall events"], suggestions.Select(item => item.Label));
    }

    [Fact]
    public void Build_UsesAssistantQuestionBeforeBroaderConversationContext()
    {
        var request = CreateRequest("Find an affordable family lunch.");

        var suggestions = ShopperReplySuggestionFallback.Build(
            request,
            "How many people are in your group?");

        Assert.Equal(["2 people", "4 people", "Family group"], suggestions.Select(item => item.Label));
    }

    private static ShopperChatRequest CreateRequest(string message) =>
        new(null, message, null, null, null);
}
