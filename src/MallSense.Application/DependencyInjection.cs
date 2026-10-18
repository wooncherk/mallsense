using Microsoft.Extensions.DependencyInjection;
using MallSense.Application.Interfaces;
using MallSense.Application.Services;

namespace MallSense.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IShopperChatService, ShopperChatService>();
        services.AddScoped<INavigationService, NavigationService>();
        services.AddScoped<INavigationGraphAdminService, NavigationGraphAdminService>();
        services.AddScoped<ITenantPromotionService, TenantPromotionService>();
        services.AddScoped<IMallAnalyticsService, MallAnalyticsService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();

        return services;
    }
}
