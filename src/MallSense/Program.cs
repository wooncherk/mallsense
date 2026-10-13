using MallSense.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddRazorPages();
builder.Services.AddScoped<HomeCatalog>();
builder.Services.AddDbContext<MallDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MallDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:MallDatabase is required.")));

var app = builder.Build();
if (args.Contains("--seed-demo", StringComparer.Ordinal))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Demo seeding is available only in Development.");
    await using var scope = app.Services.CreateAsyncScope();
    await DemoSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<MallDbContext>());
    return;
}
app.UseExceptionHandler();
app.UseStaticFiles();
app.MapRazorPages();
app.MapHealthChecks("/health");

// --seed-demo is an explicit, development-only schema/seed operation.
app.Run();
