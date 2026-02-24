using EventHub.Api.Endpoints;
using EventHub.Api.Middleware;
using EventHub.Application;
using EventHub.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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
// Endpoints
// -----------------------------------------------------------------------
app.MapEventEndpoints();
app.MapInvitationEndpoints();

app.Run();

// Exposed for WebApplicationFactory in functional tests
public partial class Program { }
