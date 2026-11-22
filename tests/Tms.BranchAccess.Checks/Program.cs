using Microsoft.EntityFrameworkCore;
using Tms.Infrastructure.Data;

var connectionString = Environment.GetEnvironmentVariable("TMS_BRANCH_FIXTURE_CONNECTION")
    ?? throw new InvalidOperationException("Set TMS_BRANCH_FIXTURE_CONNECTION to the disposable fixture database.");
var options = new DbContextOptionsBuilder<TmsDbContext>().UseNpgsql(connectionString).Options;
await using var db = new TmsDbContext(options);
var runtimeConnection = Environment.GetEnvironmentVariable("TMS_BRANCH_RUNTIME_CONNECTION")
    ?? throw new InvalidOperationException("Set TMS_BRANCH_RUNTIME_CONNECTION to the fixture runtime role connection.");
var runtimeOptions = new DbContextOptionsBuilder<TmsDbContext>().UseNpgsql(runtimeConnection).Options;
await using var runtimeDb = new TmsDbContext(runtimeOptions);
await runtimeDb.Database.OpenConnectionAsync();
var reader = new BranchAccessReader(runtimeDb);
var tenant = Guid.Parse("10000000-0000-0000-0000-000000000001");
var otherTenant = Guid.Parse("10000000-0000-0000-0000-000000000002");
var chicago = Guid.Parse("30000000-0000-0000-0000-000000000001");
var dallas = Guid.Parse("30000000-0000-0000-0000-000000000002");
var frankfurt = Guid.Parse("30000000-0000-0000-0000-000000000003");
const string issuer = "https://identity.example.invalid";

await CheckAsync("assigned branch", tenant, chicago, "CHI");
await CheckAsync("sibling branch", tenant, dallas, null);
await CheckAsync("other tenant", otherTenant, frankfurt, null);
await CheckAsync("mismatched tenant and branch", tenant, frankfurt, null);
await CheckAsync("unrecognized issuer", tenant, chicago, null, "https://other.example.invalid");
try
{
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET all_branches = true WHERE tenant_id = {tenant}");
    await CheckAsync("all branches within tenant", tenant, dallas, "DAL");
    await CheckAsync("all branches does not cross tenants", otherTenant, frankfurt, null);
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET is_active = false WHERE tenant_id = {tenant}");
    await CheckAsync("inactive membership", tenant, chicago, null);
}
finally
{
    await db.Database.ExecuteSqlInterpolatedAsync(
        $"UPDATE memberships SET is_active = true, all_branches = false WHERE tenant_id = {tenant}");
}

var visibleWithoutContext = await runtimeDb.Database
    .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM branches""").SingleAsync();
if (visibleWithoutContext != 0)
{
    throw new InvalidOperationException("Runtime role saw branches without transaction context.");
}
Console.WriteLine("PASS RLS denies context-free access on reused connection");
await BranchHttpChecks.RunAsync(runtimeConnection, issuer, tenant, otherTenant, chicago, dallas, frankfurt);

async Task CheckAsync(string scenario, Guid tenantId, Guid branchId, string? expectedCode,
    string tokenIssuer = issuer)
{
    var branch = await reader.FindAsync(tokenIssuer, "demo-operator", tenantId, branchId, CancellationToken.None);
    if (branch?.Code != expectedCode)
    {
        throw new InvalidOperationException($"{scenario}: expected {expectedCode ?? "no branch"}, got {branch?.Code ?? "no branch"}.");
    }

    Console.WriteLine($"PASS {scenario}");
}
