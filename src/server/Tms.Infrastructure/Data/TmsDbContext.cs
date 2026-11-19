using Microsoft.EntityFrameworkCore;

namespace Tms.Infrastructure.Data;

public sealed class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
}
