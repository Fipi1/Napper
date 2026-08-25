using Napper.Components;
using Microsoft.EntityFrameworkCore;
using Napper.Data;
using Napper.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDbContext<NapperDbContext>(options =>
{
    var databaseUrl = builder.Configuration["DATABASE_URL"];
    var configuredConnectionString = builder.Configuration.GetConnectionString("NapperDatabase");
    var connectionString = string.IsNullOrWhiteSpace(databaseUrl) ? configuredConnectionString : NormalizeConnectionString(databaseUrl);

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

static string NormalizeConnectionString(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(userInfo[0])
        };

        if (userInfo.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        if (!string.IsNullOrWhiteSpace(uri.Query))
        {
            var query = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in query)
            {
                var pair = part.Split('=', 2);
                var key = pair[0];
                var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;

                if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                {
                    builder.SslMode = Enum.TryParse<SslMode>(value, true, out var sslMode)
                        ? sslMode
                        : SslMode.Require;
                }
                else if (key.Equals("trust server certificate", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("trust_server_certificate", StringComparison.OrdinalIgnoreCase))
                {
                    builder.TrustServerCertificate = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return builder.ConnectionString;
    }

    return connectionString;
}
