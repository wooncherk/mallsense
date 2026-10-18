using Microsoft.Extensions.FileProviders;
using MallSense.Application;
using MallSense.Infrastructure;
using MallSense.Infrastructure.Data;
using MallSense.WebApi.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Use appsettings.json as the sole source for application settings. This prevents
// environment variables, command-line values, user secrets, or environment-specific
// JSON files from silently overriding the checked configuration.
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Console/debug logging works in local and hosted environments without requiring Windows Event Log privileges.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add Clean Architecture service layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<BiSummaryScheduler>();

// Add API Controllers and OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add CORS policy for local development / frontend apps
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Create the local SQLite schema before serving requests. Development data is seeded below.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MallDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await dbContext.EnsureTenantSchemaAsync();
    await dbContext.EnsureShopperConversationSchemaAsync();
    await dbContext.EnsureBehaviorAnalyticsSchemaAsync();
    if (app.Environment.IsDevelopment())
    {
        await MallDbContextSeed.SeedAsync(dbContext);
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sunway Kluang Mall Intelligence API")
               .WithTheme(ScalarTheme.Moon);
    });
}

app.UseCors();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// Serve React production UI if built
var candidatePaths = new[]
{
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../client/dist")),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../client/dist")),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "client/dist")),
    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "client/dist")),
};
var clientDist = candidatePaths.FirstOrDefault(Directory.Exists);
if (clientDist != null)
{
    var fileProvider = new PhysicalFileProvider(clientDist);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

    // Fallback for SPA routing
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });
}
else
{
    // Redirect root to OpenAPI docs UI if client is not built
    app.MapGet("/", () => Results.Redirect("/scalar/v1"));
}

app.MapControllers();

app.Run();
