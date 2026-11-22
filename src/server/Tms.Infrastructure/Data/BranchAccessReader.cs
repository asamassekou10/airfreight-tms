using Microsoft.EntityFrameworkCore;

namespace Tms.Infrastructure.Data;

public sealed record AccessibleBranch(Guid Id, string Code, string Name);

public sealed class BranchAccessReader(TmsDbContext db)
{
    public async Task<AccessibleBranch?> FindAsync(
        string issuer, string subject, Guid tenantId, Guid branchId,
        CancellationToken cancellationToken)
    {
        var scope = new RequestScope(tenantId, branchId, issuer, subject);
        await using var transaction = await scope.BeginAsync(db, cancellationToken);

        var branch = await db.Database.SqlQuery<AccessibleBranch>($"""
            SELECT b.id AS "Id", b.code AS "Code", b.name AS "Name"
            FROM branches b
            JOIN tenants t ON t.id = b.tenant_id
            WHERE b.id = {branchId}
              AND b.tenant_id = {tenantId}
              AND b.is_active AND t.is_active
              AND EXISTS (
                  SELECT 1
                  FROM memberships m
                  JOIN users u ON u.id = m.user_id
                  WHERE m.tenant_id = b.tenant_id
                    AND m.is_active AND u.is_active
                    AND u.identity_issuer = {issuer}
                    AND u.identity_subject = {subject}
                    AND (m.all_branches OR EXISTS (
                        SELECT 1
                        FROM membership_branches mb
                        WHERE mb.membership_id = m.id
                          AND mb.tenant_id = b.tenant_id
                          AND mb.branch_id = b.id
                    ))
              )
            """).SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return branch;
    }
}
