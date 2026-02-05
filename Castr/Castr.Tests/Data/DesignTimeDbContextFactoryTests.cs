using Castr.Data;
using Microsoft.EntityFrameworkCore;

namespace Castr.Tests.Data;

public class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_DefaultsSQLite_WhenNoEnvironmentVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EF_PROVIDER", null);
        Environment.SetEnvironmentVariable("EF_CONNECTION", null);
        var factory = new DesignTimeDbContextFactory();

        // Act
        var context = factory.CreateDbContext(Array.Empty<string>());

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Database.IsSqlite());
        context.Dispose();
    }

    [Fact]
    public void CreateDbContext_UsesEnvironmentVariables_WhenProvided()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EF_PROVIDER", "SQLite");
        Environment.SetEnvironmentVariable("EF_CONNECTION", "Data Source=test-design.db");
        var factory = new DesignTimeDbContextFactory();

        // Act
        var context = factory.CreateDbContext(Array.Empty<string>());

        // Assert
        Assert.NotNull(context);
        Assert.True(context.Database.IsSqlite());
        context.Dispose();

        // Cleanup
        Environment.SetEnvironmentVariable("EF_PROVIDER", null);
        Environment.SetEnvironmentVariable("EF_CONNECTION", null);
    }
}
