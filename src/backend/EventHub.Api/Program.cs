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
// Authentication — Azure Entra ID JWT Bearer
// -----------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AzureAd:Authority"];
        options.Audience  = builder.Configuration["AzureAd:Audience"];
    });

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
