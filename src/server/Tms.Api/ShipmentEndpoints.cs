using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tms.Infrastructure.Data;

namespace Tms.Api;

internal static class ShipmentEndpoints
{
    public static void MapShipmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId:guid}/branches/{branchId:guid}/shipments")
            .RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapPost("", (Guid tenantId, Guid branchId, ClaimsPrincipal user, DraftInput input,
            TmsDbContext db, CancellationToken ct) => SaveAsync(tenantId, branchId, null, user, input, db, ct));
        group.MapPut("/{id:guid}", (Guid tenantId, Guid branchId, Guid id, ClaimsPrincipal user, DraftInput input,
            TmsDbContext db, CancellationToken ct) => SaveAsync(tenantId, branchId, id, user, input, db, ct));
        group.MapPost("/{id:guid}/validate", ValidateAsync);
    }

    private static async Task<IResult> ListAsync(Guid tenantId, Guid branchId, ClaimsPrincipal user,
        TmsDbContext db, CancellationToken ct)
    {
        var scope = PartyEndpoints.GetScope(tenantId, branchId, user);
        if (scope is null) return Results.Unauthorized();
        await using var transaction = await scope.BeginAsync(db, ct);
        var access = await CheckAccessAsync(tenantId, branchId, db, ct);
        if (access is not null) return access;
        var drafts = await db.Database.SqlQuery<Draft>($"""
            SELECT id AS "Id", version AS "Version", status AS "Status", consignee_id AS "ConsigneeId",
                origin AS "Origin", destination AS "Destination", gross_weight_kg AS "GrossWeightKg"
            FROM shipment_drafts WHERE tenant_id = {tenantId} AND branch_id = {branchId}
            """).OrderBy(draft => draft.Id).Take(100).ToListAsync(ct);
        return Results.Ok(drafts);
    }

    private static async Task<IResult> SaveAsync(Guid tenantId, Guid branchId, Guid? id,
        ClaimsPrincipal user, DraftInput input, TmsDbContext db, CancellationToken ct)
    {
        var scope = PartyEndpoints.GetScope(tenantId, branchId, user);
        if (scope is null) return Results.Unauthorized();
        if (input.GrossWeightKg <= 0 || (id.HasValue && input.Version is not > 0))
            return Results.BadRequest(new { error = "Supply a positive weight and the current version when editing." });

        await using var transaction = await scope.BeginAsync(db, ct);
        var access = await CheckAccessAsync(tenantId, branchId, db, ct);
        if (access is not null) return access;
        var shipmentId = id ?? Guid.NewGuid();
        try
        {
            var count = id.HasValue
                ? await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE shipment_drafts SET consignee_id = {input.ConsigneeId}, origin = {input.Origin},
                        destination = {input.Destination}, gross_weight_kg = {input.GrossWeightKg},
                        version = version + 1
                    WHERE id = {shipmentId} AND tenant_id = {tenantId} AND branch_id = {branchId}
                        AND version = {input.Version} AND status = 'draft'
                    """, ct)
                : await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO shipment_drafts (id, tenant_id, branch_id, consignee_id, origin, destination, gross_weight_kg)
                    VALUES ({shipmentId}, {tenantId}, {branchId}, {input.ConsigneeId}, {input.Origin},
                        {input.Destination}, {input.GrossWeightKg})
                    """, ct);
            if (count != 1)
                return await ReadAsync(tenantId, branchId, shipmentId, db, ct) is null
                    ? Results.NotFound() : Results.Conflict();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return Results.BadRequest(new { error = "Choose an approved consignee in this branch and valid airport codes." });
        }

        var draft = await ReadAsync(tenantId, branchId, shipmentId, db, ct);
        await transaction.CommitAsync(ct);
        return Results.Json(draft, statusCode: id.HasValue ? 200 : 201);
    }

    private static async Task<IResult> ValidateAsync(Guid tenantId, Guid branchId, Guid id, ClaimsPrincipal user,
        ValidateInput input, TmsDbContext db, CancellationToken ct)
    {
        var scope = PartyEndpoints.GetScope(tenantId, branchId, user);
        if (scope is null) return Results.Unauthorized();
        await using var transaction = await scope.BeginAsync(db, ct);
        var access = await CheckAccessAsync(tenantId, branchId, db, ct);
        if (access is not null) return access;
        var draft = await ReadAsync(tenantId, branchId, id, db, ct);
        if (draft is null) return Results.NotFound();
        if (draft.Version != input.Version || draft.Status != "draft") return Results.Conflict();
        var errors = new Dictionary<string, string[]>();
        if (draft.ConsigneeId is null) errors["consigneeId"] = ["Select an approved consignee."];
        if (draft.Origin is null) errors["origin"] = ["Select an origin airport."];
        if (draft.Destination is null || draft.Destination == draft.Origin)
            errors["destination"] = ["Select a destination airport different from the origin."];
        if (draft.GrossWeightKg is null) errors["grossWeightKg"] = ["Enter gross weight."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE shipment_drafts SET status = 'validated', version = version + 1
            WHERE id = {id} AND tenant_id = {tenantId} AND branch_id = {branchId}
                AND version = {input.Version} AND status = 'draft'
            """, ct);
        if (updated != 1) return Results.Conflict();
        var audited = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO audit_events (id, tenant_id, branch_id, actor_id, action, entity_id)
            SELECT {Guid.NewGuid()}, {tenantId}, {branchId}, id, 'shipment.validated', {id} FROM users
            """, ct);
        if (audited != 1) return Results.NotFound();
        await transaction.CommitAsync(ct);
        return Results.Ok(draft with { Status = "validated", Version = draft.Version + 1 });
    }

    private static async Task<IResult?> CheckAccessAsync(Guid tenantId, Guid branchId, TmsDbContext db, CancellationToken ct)
    {
        var branch = await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM branches WHERE id = {branchId} AND tenant_id = {tenantId}) AS "Value"
            """).SingleAsync(ct);
        if (!branch) return Results.NotFound();
        var allowed = await db.Database.SqlQuery<bool>($"""
            SELECT has_m1_permission('shipment.write') AS "Value"
            """).SingleAsync(ct);
        return allowed ? null : Results.Forbid();
    }

    private static Task<Draft?> ReadAsync(Guid tenantId, Guid branchId, Guid id, TmsDbContext db, CancellationToken ct) =>
        db.Database.SqlQuery<Draft>($"""
            SELECT id AS "Id", version AS "Version", status AS "Status", consignee_id AS "ConsigneeId",
                origin AS "Origin", destination AS "Destination", gross_weight_kg AS "GrossWeightKg"
            FROM shipment_drafts WHERE id = {id} AND tenant_id = {tenantId} AND branch_id = {branchId}
            """).SingleOrDefaultAsync(ct);

    private sealed record DraftInput(Guid? ConsigneeId, string? Origin, string? Destination, decimal? GrossWeightKg, int? Version);
    private sealed record ValidateInput(int Version);
    private sealed record Draft(Guid Id, int Version, string Status, Guid? ConsigneeId, string? Origin,
        string? Destination, decimal? GrossWeightKg);
}
