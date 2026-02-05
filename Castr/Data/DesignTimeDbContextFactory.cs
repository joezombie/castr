using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Castr.Data;

/// <summary>
/// Factory for creating CastrDbContext at design time for EF Core migrations.
/// Used by dotnet-ef CLI commands.
///
/// Configure provider via environment variables:
/// - EF_PROVIDER: SQLite (default), PostgreSQL, SqlServer, MariaDB
/// - EF_CONNECTION: Connection string (default: Data Source=castr-design.db)
///
/// Example usage:
///   EF_PROVIDER=PostgreSQL EF_CONNECTION="Host=localhost;Database=castr" dotnet ef migrations add MyMigration
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CastrDbContext>
{
    public CastrDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("EF_PROVIDER") ?? "SQLite";
        var connectionString = Environment.GetEnvironmentVariable("EF_CONNECTION")
            ?? "Data Source=castr-design.db";

        var optionsBuilder = new DbContextOptionsBuilder<CastrDbContext>();

        switch (provider.ToLowerInvariant())
        {
            case "postgresql":
            case "postgres":
                optionsBuilder.UseNpgsql(connectionString);
                break;

            case "sqlserver":
            case "mssql":
                optionsBuilder.UseSqlServer(connectionString);
                break;

            case "mysql":
            case "mariadb":
                // For design-time, use a default MySQL 8.0 version
                var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
                optionsBuilder.UseMySql(connectionString, serverVersion);
                break;

            case "sqlite":
            default:
                optionsBuilder.UseSqlite(connectionString);
                break;
        }

        return new CastrDbContext(optionsBuilder.Options);
    }
}
