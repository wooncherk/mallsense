using MallSense.Application.DTOs;

namespace MallSense.Application.Interfaces;

public interface IShopperChatService
{
    Task<ShopperChatResponse> ProcessShopperMessageAsync(
        ShopperChatRequest request,
        CancellationToken cancellationToken = default,
        Func<ShopperChatProgressDto, CancellationToken, Task>? progress = null);
}
