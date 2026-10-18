using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MallSense.Application.Interfaces;
using MallSense.Domain.Interfaces;
using MallSense.Infrastructure.AI;
using MallSense.Infrastructure.Data;
using MallSense.Infrastructure.Navigation;
using MallSense.Infrastructure.Repositories;

namespace MallSense.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // SQLite keeps application data across restarts. Tests opt into the in-memory provider separately.
        var connectionString = configuration.GetConnectionString("MallSense")
            ?? "Data Source=kluangmall.db";
        services.AddDbContext<MallDbContext>(options =>
            options.UseSqlite(connectionString));

        // Register repositories
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INavigationGraphRepository, NavigationGraphRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IIntentSignalRepository, IntentSignalRepository>();
        services.AddScoped<IShopperConversationRepository, ShopperConversationRepository>();
        services.AddScoped<IShopperBehaviorEventRepository, ShopperBehaviorEventRepository>();
        services.AddScoped<IFacilityRepository, FacilityRepository>();
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();

        // Register deterministic pathfinder
        services.AddSingleton<IPathFindingService, AStarPathFinder>();

        // Multi-floor plan analysis can require several large image requests and a final reconciliation pass.
        var geminiTimeoutSeconds = Math.Max(100, configuration.GetValue<int?>("Gemini:RequestTimeoutSeconds") ?? 600);
        services.AddHttpClient<IGeminiClient, GeminiApiClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(geminiTimeoutSeconds));

        // JWT Bearer authentication
        var jwtKey = configuration["Jwt:Key"] ?? "MallSense_Super_Secret_Key_For_Dev_2026_Do_Not_Use_In_Production!";
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "MallSense",
                    ValidAudience = configuration["Jwt:Audience"] ?? "MallSenseClient",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        services.AddAuthorization();

        return services;
    }
}
