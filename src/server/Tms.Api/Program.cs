using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tms.Infrastructure.Data;
using Tms.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
var branchLookupEnabled = !string.IsNullOrWhiteSpace(authority) && !string.IsNullOrWhiteSpace(audience);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<BranchAccessReader>();
builder.Services.AddScoped<PartyApprovalStore>();

var connectionString = builder.Configuration.GetConnectionString("Tms");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The database connection string is missing. Set ConnectionStrings__Tms for local development.");
}

builder.Services.AddDbContext<TmsDbContext>(options => options.UseNpgsql(connectionString));
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<TmsDbContext>("postgres", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

if (branchLookupEnabled)
{
    app.MapGet("/api/tenants/{tenantId:guid}/branches/{branchId:guid}", ReadBranchAsync)
        .RequireAuthorization();
    app.MapPartyEndpoints();
}

app.Run();

static async Task<IResult> ReadBranchAsync(
    Guid tenantId, Guid branchId, ClaimsPrincipal user, BranchAccessReader reader,
    CancellationToken cancellationToken)
{
    var issuer = user.FindFirstValue("iss");
    var subject = user.FindFirstValue("sub");
    if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
    {
        return Results.Unauthorized();
    }

    var branch = await reader.FindAsync(issuer, subject, tenantId, branchId, cancellationToken);
    return branch is null ? Results.NotFound() : Results.Ok(branch);
}

namespace Tms.Api
{
    public sealed class ApiAssemblyMarker { }
}
