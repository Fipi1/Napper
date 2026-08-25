using Napper.Components;
using Microsoft.EntityFrameworkCore;
using Napper.Data;
using Napper.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDbContext<NapperDbContext>(options =>
{
    var databaseUrl = builder.Configuration["DATABASE_URL"];
    var configuredConnectionString = builder.Configuration.GetConnectionString("NapperDatabase");
    var connectionString = string.IsNullOrWhiteSpace(databaseUrl) ? configuredConnectionString : databaseUrl;

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlite("Data Source=napper.db");
        return;
    }

    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
        return;
    }

    if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Filename=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
        return;
    }

    options.UseSqlServer(connectionString);
});
builder.Services.AddScoped<BabySleepAppState>();
builder.Services.AddScoped<SleepRecommendationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NapperDbContext>();
    await NapperDbSeeder.SeedAsync(dbContext);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
