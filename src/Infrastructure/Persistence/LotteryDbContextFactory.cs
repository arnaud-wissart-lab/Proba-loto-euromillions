using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public sealed class LotteryDbContextFactory : IDesignTimeDbContextFactory<LotteryDbContext>
{
    public LotteryDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ??
            "Host=localhost;Port=5432;Database=probabilites_loto;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<LotteryDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure());

        return new LotteryDbContext(optionsBuilder.Options);
    }
}
