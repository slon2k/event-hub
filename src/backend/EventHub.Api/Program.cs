using EventHub.Api.Endpoints;
using EventHub.Api.Middleware;
using EventHub.Application;
using EventHub.Infrastructure;
using EventHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// Application + Infrastructure
// -----------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// -----------------------------------------------------------------------
// Authentication — Azure Entra ID or local Dev JWT (user-jwts)
// -----------------------------------------------------------------------
var authMode = builder.Configuration["Authentication:Mode"] ?? "AzureAd";

var authenticationBuilder = builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

if (string.Equals(authMode, "DevJwt", StringComparison.OrdinalIgnoreCase))
{
    authenticationBuilder.AddJwtBearer();
}
else
{
    var authority = builder.Configuration["AzureAd:Authority"]
        ?? throw new InvalidOperationException("AzureAd:Authority is not configured.");
    var audience = builder.Configuration["AzureAd:Audience"]
        ?? throw new InvalidOperationException("AzureAd:Audience is not configured.");

    authenticationBuilder.AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            // Support both v1.0 (sts.windows.net) and v2.0 (login.microsoftonline.com) tokens.
            // v1.0 tokens are issued when the API app registration has accessTokenAcceptedVersion=null.
            // v2.0 tokens are issued when accessTokenAcceptedVersion=2.
            ValidIssuers = DeriveValidIssuers(authority)
        };
    });
}

// -----------------------------------------------------------------------
// Authorization policies
// -----------------------------------------------------------------------
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("OrganizerPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Organizer", "Admin"))
    .AddPolicy("AdminPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Admin"));

// -----------------------------------------------------------------------
// Health checks
// -----------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EventHubDbContext>("sql", HealthStatus.Unhealthy);

// -----------------------------------------------------------------------
// Problem Details + global exception handler
// -----------------------------------------------------------------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// -----------------------------------------------------------------------
// OpenAPI
// -----------------------------------------------------------------------
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// -----------------------------------------------------------------------
// Health checks
// -----------------------------------------------------------------------
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name    = e.Key,
                status  = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
}).AllowAnonymous();

// -----------------------------------------------------------------------
// Endpoints
// -----------------------------------------------------------------------
app.MapEventEndpoints();
app.MapInvitationEndpoints();

app.Run();

// Derives both v1.0 and v2.0 valid issuers from the authority URL.
// authority format: https://login.microsoftonline.com/{tenantId}/v2.0
static string[] DeriveValidIssuers(string authority)
{
    var tenantId = new Uri(authority.TrimEnd('/')).Segments
        .Select(s => s.Trim('/'))
        .FirstOrDefault(s => s != string.Empty && s != "v2.0")
        ?? string.Empty;

    return
    [
        $"https://login.microsoftonline.com/{tenantId}/v2.0",   // v2.0 tokens
        $"https://sts.windows.net/{tenantId}/"                  // v1.0 tokens
    ];
}

// Exposed for WebApplicationFactory in functional tests
public partial class Program { }
