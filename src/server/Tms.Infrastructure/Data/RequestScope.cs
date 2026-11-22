using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Tms.Infrastructure.Data;

public sealed record RequestScope(Guid TenantId, Guid BranchId, string Issuer, string Subject)
{
    public async Task<IDbContextTransaction> BeginAsync(TmsDbContext db, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT set_config('tms.tenant_id', {TenantId.ToString("D")}, true),
                       set_config('tms.identity_issuer', {Issuer}, true),
                       set_config('tms.identity_subject', {Subject}, true)
                """, cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
