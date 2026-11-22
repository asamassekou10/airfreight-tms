using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Tms.Infrastructure.Data;

public sealed record Consignee(Guid Id, string Name);
public sealed record PartyRequest(Guid Id, string Name, string Status);
public sealed record AuditEvent(Guid Id, Guid ActorId, Guid EntityId, string Action, string? Reason, DateTime OccurredAt);

public enum PartyWriteOutcome { Success, Inaccessible, Forbidden, Conflict }

public sealed class PartyApprovalStore(TmsDbContext db)
{
    public async Task<(PartyWriteOutcome Access, IReadOnlyList<AuditEvent> Events)> ListAuditAsync(
        RequestScope scope, CancellationToken ct)
    {
        await using var transaction = await scope.BeginAsync(db, ct);
        var access = await CheckPermissionAsync(scope, "party.approve", ct);
        if (access != PartyWriteOutcome.Success)
        {
            return (access, []);
        }

        var events = await db.Database.SqlQuery<AuditEvent>($"""
            SELECT id AS "Id", actor_id AS "ActorId", entity_id AS "EntityId",
                action AS "Action", reason AS "Reason", occurred_at AS "OccurredAt"
            FROM audit_events WHERE tenant_id = {scope.TenantId} AND branch_id = {scope.BranchId}
            """).OrderByDescending(entry => entry.OccurredAt).ThenBy(entry => entry.Id).Take(100).ToListAsync(ct);
        return (access, events);
    }

    public async Task<IReadOnlyList<Consignee>?> ListConsigneesAsync(RequestScope scope, CancellationToken ct)
    {
        await using var transaction = await scope.BeginAsync(db, ct);
        if (!await CanReadBranchAsync(scope, ct))
        {
            return null;
        }

        return await db.Database.SqlQuery<Consignee>($"""
            SELECT id AS "Id", name AS "Name" FROM approved_consignees
            WHERE tenant_id = {scope.TenantId} AND branch_id = {scope.BranchId}
            """).OrderBy(party => party.Name).ThenBy(party => party.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PartyRequest>?> ListRequestsAsync(RequestScope scope, CancellationToken ct)
    {
        await using var transaction = await scope.BeginAsync(db, ct);
        if (!await CanReadBranchAsync(scope, ct))
        {
            return null;
        }

        return await db.Database.SqlQuery<PartyRequest>($"""
            SELECT id AS "Id", name AS "Name", status AS "Status" FROM party_requests
            WHERE tenant_id = {scope.TenantId} AND branch_id = {scope.BranchId}
            """).OrderBy(request => request.Name).ThenBy(request => request.Id).ToListAsync(ct);
    }

    public async Task<PartyWriteOutcome> RequestAsync(RequestScope scope, Guid requestId, string name, CancellationToken ct)
    {
        await using var transaction = await scope.BeginAsync(db, ct);
        var access = await CheckPermissionAsync(scope, "party.request", ct);
        if (access != PartyWriteOutcome.Success)
        {
            return access;
        }

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO party_requests (id, tenant_id, branch_id, requester_id, name)
            SELECT {requestId}, {scope.TenantId}, {scope.BranchId}, id, {name} FROM users
            """, ct);
        if (inserted != 1 || await AppendAuditAsync(scope, requestId, "party.requested", reason: null, ct) != 1)
        {
            return PartyWriteOutcome.Inaccessible;
        }
        await transaction.CommitAsync(ct);
        return PartyWriteOutcome.Success;
    }

    public async Task<PartyWriteOutcome> DecideAsync(RequestScope scope, Guid requestId, string reason, bool approve, CancellationToken ct)
    {
        await using var transaction = await scope.BeginAsync(db, ct);
        var access = await CheckPermissionAsync(scope, "party.approve", ct);
        if (access != PartyWriteOutcome.Success)
        {
            return access;
        }

        var request = await db.Database.SqlQuery<PartyRequest>($"""
            SELECT id AS "Id", name AS "Name", status AS "Status" FROM party_requests
            WHERE id = {requestId} AND tenant_id = {scope.TenantId} AND branch_id = {scope.BranchId}
            """).SingleOrDefaultAsync(ct);
        if (request is null)
        {
            return PartyWriteOutcome.Inaccessible;
        }

        var status = approve ? "approved" : "rejected";
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE party_requests SET status = {status}
            WHERE id = {requestId} AND tenant_id = {scope.TenantId} AND branch_id = {scope.BranchId}
              AND status = 'pending'
            """, ct);
        if (updated != 1)
        {
            return PartyWriteOutcome.Conflict;
        }

        if (approve)
        {
            var result = await CreateConsigneeAsync(scope, request, ct);
            if (result != PartyWriteOutcome.Success) return result;
        }

        if (await AppendAuditAsync(scope, requestId, $"party.{status}", reason, ct) != 1)
        {
            return PartyWriteOutcome.Inaccessible;
        }
        await transaction.CommitAsync(ct);
        return PartyWriteOutcome.Success;
    }

    private async Task<PartyWriteOutcome> CreateConsigneeAsync(RequestScope scope, PartyRequest request, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO approved_consignees (id, tenant_id, branch_id, name)
                VALUES ({request.Id}, {scope.TenantId}, {scope.BranchId}, {request.Name})
                """, ct);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "approved_consignee_name")
        {
            return PartyWriteOutcome.Conflict;
        }

        return PartyWriteOutcome.Success;
    }

    private Task<bool> CanReadBranchAsync(RequestScope scope, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM branches WHERE id = {scope.BranchId} AND tenant_id = {scope.TenantId}
            ) AS "Value"
            """).SingleAsync(ct);

    private async Task<PartyWriteOutcome> CheckPermissionAsync(RequestScope scope, string permission, CancellationToken ct)
    {
        if (!await CanReadBranchAsync(scope, ct))
        {
            return PartyWriteOutcome.Inaccessible;
        }

        var allowed = await db.Database.SqlQuery<bool>($"""
            SELECT has_m1_permission({permission}) AS "Value"
            """).SingleAsync(ct);
        return allowed ? PartyWriteOutcome.Success : PartyWriteOutcome.Forbidden;
    }

    private Task<int> AppendAuditAsync(RequestScope scope, Guid entityId, string action, string? reason, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO audit_events (id, tenant_id, branch_id, actor_id, action, entity_id, reason)
            SELECT {Guid.NewGuid()}, {scope.TenantId}, {scope.BranchId}, id, {action}, {entityId}, {reason} FROM users
            """, ct);
}
