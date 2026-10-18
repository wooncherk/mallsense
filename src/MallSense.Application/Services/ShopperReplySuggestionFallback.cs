using MallSense.Application.DTOs;

namespace MallSense.Application.Services;

public static class ShopperReplySuggestionFallback
{
    public static IReadOnlyList<ShopperReplySuggestionDto> Build(
        ShopperChatRequest request,
        string assistantReply)
    {
        var conversation = string.Join(' ', request.Conversation?.Select(turn => turn.Message) ?? []);
        var context = $"{conversation} {request.Message} {assistantReply}";

        if (ContainsAny(assistantReply, "quick bite", "sit-down", "sit down") &&
            ContainsAny(assistantReply, "cafe", "café", "beverage", "drink"))
            return
            [
                new("Quick bite", "I am looking for a quick bite. Recommend the best suitable options from what is currently available."),
                new("Cafe beverage", "I would like a cafe beverage. Recommend suitable cafes and current offers."),
                new("Sit-down meal", "I prefer a sit-down meal. Recommend the best suitable restaurants and current offers.")
            ];

        if (ContainsAny(assistantReply, "how many", "group"))
        {
            var groupChoices = new List<ShopperReplySuggestionDto>
            {
                new("2 people", "There will be two people. Recommend suitable options for us."),
                new("4 people", "There will be four people. Recommend suitable options for our group.")
            };
            groupChoices.Add(ContainsAny(assistantReply, "diet", "dietary")
                ? new ShopperReplySuggestionDto("No dietary needs", "We have no specific dietary requirements. Please recommend the best suitable options.")
                : new ShopperReplySuggestionDto("Family group", "We are visiting as a family. Prioritize comfortable family-friendly options."));
            return groupChoices;
        }

        if (ContainsAny(assistantReply, "diet", "dietary"))
            return
            [
                new("No dietary needs", "We have no specific dietary requirements. Please recommend the best suitable options."),
                new("Halal options", "We need halal dining options. Recommend suitable choices for us."),
                new("Vegetarian options", "We prefer vegetarian options. Recommend suitable choices for us.")
            ];

        if (ContainsAny(assistantReply, "budget", "price", "spend", "cost"))
            return
            [
                new("Budget-friendly", "Please prioritize affordable, budget-friendly options."),
                new("Mid-range", "We prefer mid-range options with good value."),
                new("Flexible budget", "Our budget is flexible. Prioritize the best match for our needs.")
            ];

        if (ContainsAny(assistantReply, "cuisine", "food", "meal", "eat", "restaurant", "dining") ||
            ContainsAny(context, "lunch", "dinner"))
            return
            [
                new("Local Malaysian", "We prefer local Malaysian food. Recommend suitable options based on our conversation."),
                new("Cafe or dessert", "We would like a cafe or dessert option. Recommend suitable choices based on our conversation."),
                new("Family-friendly", "Please prioritize family-friendly dining options for our group.")
            ];

        if (ContainsAny(context, "deal", "promotion", "discount", "offer", "event"))
            return
            [
                new("Dining deals", "Show me the best current dining deals that match what we discussed."),
                new("Shopping deals", "Show me the best current shopping deals that match what we discussed."),
                new("Mall events", "Show me current mall events that may interest me.")
            ];

        if (ContainsAny(context, "store", "shop", "product", "gift"))
            return
            [
                new("Popular choices", "Show me the most popular choices that match what I asked for."),
                new("Best value", "Prioritize the best-value stores or products that match my request."),
                new("Nearest stores", "Prioritize matching stores that are easiest to reach in the mall.")
            ];

        return
        [
            new("Show top choices", "Please narrow this down to the best two or three choices for me."),
            new("Tell me more", "Please give me more detail about the most relevant option you just mentioned."),
            new("Other options", "Please suggest a few different options based on what we discussed.")
        ];
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
