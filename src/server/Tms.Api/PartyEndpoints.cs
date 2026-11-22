using System.Security.Claims;
using Tms.Infrastructure.Data;

namespace Tms.Api;

internal static class PartyEndpoints
{
    public static void MapPartyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId:guid}/branches/{branchId:guid}")
            .RequireAuthorization();

        group.MapGet("/audit-events", async (Guid tenantId, Guid branchId, ClaimsPrincipal user,
            PartyApprovalStore store, CancellationToken ct) =>
        {
            var scope = GetScope(tenantId, branchId, user);
            if (scope is null) return Results.Unauthorized();
            var (access, events) = await store.ListAuditAsync(scope, ct);
            return ToResult(access, Results.Ok(events));
        });

        group.MapGet("/consignees", async (Guid tenantId, Guid branchId, ClaimsPrincipal user,
            PartyApprovalStore store, CancellationToken ct) =>
        {
            var scope = GetScope(tenantId, branchId, user);
            if (scope is null) return Results.Unauthorized();
            var parties = await store.ListConsigneesAsync(scope, ct);
            return parties is null ? Results.NotFound() : Results.Ok(parties);
        });

        group.MapGet("/party-requests", async (Guid tenantId, Guid branchId, ClaimsPrincipal user,
            PartyApprovalStore store, CancellationToken ct) =>
        {
            var scope = GetScope(tenantId, branchId, user);
            if (scope is null) return Results.Unauthorized();
            var requests = await store.ListRequestsAsync(scope, ct);
            return requests is null ? Results.NotFound() : Results.Ok(requests);
        });

        group.MapPost("/party-requests", async (Guid tenantId, Guid branchId, ClaimsPrincipal user,
            CreatePartyRequest body, PartyApprovalStore store, CancellationToken ct) =>
        {
            var scope = GetScope(tenantId, branchId, user);
            if (scope is null) return Results.Unauthorized();
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 200)
                return Results.BadRequest(new { error = "Enter a consignee name up to 200 characters." });
            var id = Guid.NewGuid();
            return ToResult(await store.RequestAsync(scope, id, name, ct),
                Results.Json(new PartyRequest(id, name, "pending"), statusCode: StatusCodes.Status201Created));
        });

        group.MapPost("/party-requests/{requestId:guid}/approve", (
            Guid tenantId, Guid branchId, Guid requestId, ClaimsPrincipal user,
            PartyDecision body, PartyApprovalStore store, CancellationToken ct) =>
            DecideAsync(tenantId, branchId, requestId, user, body, store, approve: true, ct));
        group.MapPost("/party-requests/{requestId:guid}/reject", (
            Guid tenantId, Guid branchId, Guid requestId, ClaimsPrincipal user,
            PartyDecision body, PartyApprovalStore store, CancellationToken ct) =>
            DecideAsync(tenantId, branchId, requestId, user, body, store, approve: false, ct));
    }

    private static async Task<IResult> DecideAsync(Guid tenantId, Guid branchId, Guid requestId,
        ClaimsPrincipal user, PartyDecision body, PartyApprovalStore store, bool approve, CancellationToken ct)
    {
        var scope = GetScope(tenantId, branchId, user);
        if (scope is null) return Results.Unauthorized();
        var reason = body.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 500)
            return Results.BadRequest(new { error = "Enter a decision reason up to 500 characters." });
        return ToResult(await store.DecideAsync(scope, requestId, reason, approve, ct), Results.NoContent());
    }

    internal static RequestScope? GetScope(Guid tenantId, Guid branchId, ClaimsPrincipal user)
    {
        var issuer = user.FindFirstValue("iss");
        var subject = user.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)
            ? null
            : new RequestScope(tenantId, branchId, issuer, subject);
    }

    private static IResult ToResult(PartyWriteOutcome outcome, IResult success) => outcome switch
    {
        PartyWriteOutcome.Success => success,
        PartyWriteOutcome.Forbidden => Results.Forbid(),
        PartyWriteOutcome.Inaccessible => Results.NotFound(),
        PartyWriteOutcome.Conflict => Results.Conflict(new { error = "The request cannot be decided or a consignee with that name already exists." }),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    private sealed record CreatePartyRequest(string? Name);
    private sealed record PartyDecision(string? Reason);
}
