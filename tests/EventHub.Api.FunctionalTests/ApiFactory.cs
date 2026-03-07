using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace EventHub.Api.FunctionalTests;

/// <summary>
/// Boots the full ASP.NET Core pipeline against a real SQL Server 2022 container.
/// Shared across all tests in the "Api" collection via ICollectionFixture.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // A stable 32-byte HMAC key used to sign and validate test JWTs.
    // Must match the "ValidAudiences" / "ValidIssuer" configured below.
    public const string TestHmacKeyBase64 = "dGVzdC1zaWduaW5nLWtleS0yNTZiaXQtMTIzNDU2NyE=";
    public const string TestIssuer = "test-issuer";
    public const string TestAudience = "test-audience";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Run migrations once before any test in the collection.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventHubDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Mode"]                       = "DevJwt",
                ["ConnectionStrings:DefaultConnection"]        = _container.GetConnectionString(),
                // Stable test HMAC key so RsvpTokenService works without real config.
                ["RsvpToken:HmacKey"]                         = TestHmacKeyBase64,
                // ServiceBus not needed — no outbox notifier registered without this.
                ["ServiceBusConnectionString"]                = "",
            });
        });

        // Explicitly configure JWT validation so tests don't rely on user-secrets signing keys.
        // PostConfigure runs after all Configure actions, guaranteeing these settings win.
        builder.ConfigureServices(services =>
        {
            // Ensure tests never use appsettings LocalDB on non-Windows runners.
            services.RemoveAll<DbContextOptions<EventHubDbContext>>();
            services.RemoveAll<EventHubDbContext>();
            services.AddDbContext<EventHubDbContext>(options =>
                options.UseSqlServer(_container.GetConnectionString(), sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 5)));

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(Convert.FromBase64String(TestHmacKeyBase64));
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidIssuer = TestIssuer,
                    ValidAudience = TestAudience,
                    // Disable default claim-type remapping so "roles" / "oid"
                    // are preserved exactly as written in the token.
                    RoleClaimType = "roles",
                    NameClaimType = "oid",
                };
                // With MapInboundClaims = false claim names in the JWT are not
                // remapped to long .NET URIs, so "roles" stays "roles".
                options.MapInboundClaims = false;
            });
        });
    }
}

[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
