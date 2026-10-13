using MallSense.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<MallDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MallDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:MallDatabase is required.")));

var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/", () => new { application = "MallSense", stage = "Data model foundation" });
app.MapHealthChecks("/health");

// Database creation/migrations are intentional operations, never startup side effects.
app.Run();
