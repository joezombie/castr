using Microsoft.EntityFrameworkCore;

namespace Castr.Data;

public class DatabaseConfiguration
{
    public string Provider { get; set; } = "SQLite";
    public string ConnectionString { get; set; } = "Data Source=castr.db";
}

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddCastrDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConfig = configuration.GetSection("Database").Get<DatabaseConfiguration>()
            ?? new DatabaseConfiguration();

        services.AddDbContext<CastrDbContext>(options =>
        {
            switch (dbConfig.Provider.ToLowerInvariant())
            {
                case "postgresql":
                case "postgres":
                    options.UseNpgsql(dbConfig.ConnectionString);
                    break;

                case "sqlserver":
                case "mssql":
                    options.UseSqlServer(dbConfig.ConnectionString);
                    break;

                case "mysql":
                case "mariadb":
                    try
                    {
                        var serverVersion = ServerVersion.AutoDetect(dbConfig.ConnectionString);
                        options.UseMySql(dbConfig.ConnectionString, serverVersion);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to auto-detect MySQL/MariaDB server version. Ensure the database is accessible. Connection string: {MaskConnectionString(dbConfig.ConnectionString)}",
                            ex);
                    }
                    break;

                case "sqlite":
                    options.UseSqlite(dbConfig.ConnectionString);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: '{dbConfig.Provider}'. " +
                        "Supported providers: sqlite, postgresql, postgres, sqlserver, mssql, mysql, mariadb");
            }
        });

        return services;
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask password in connection string for safe logging
        if (string.IsNullOrEmpty(connectionString))
            return "(empty)";

        var masked = System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            @"(password|pwd)\s*=\s*[^;]+",
            "$1=***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return masked;
    }
}
