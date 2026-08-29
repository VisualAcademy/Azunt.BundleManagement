using Azunt.BundleManagement;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Azunt.BundleManagement.Tests;

public sealed class BundleInfrastructureTests
{
    [Fact]
    public async Task BundlesTableBuilder_RequiresExplicitConnectionString()
    {
        var builder = new BundlesTableBuilder(NullLogger<BundlesTableBuilder>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => builder.EnsureAsync(string.Empty));
    }

    [Fact]
    public void SqlServerFactory_DoesNotFallBackToInMemory_WhenConfiguredForTenantSqlMode()
    {
        var factory = new BundleAppDbContextFactory(
            connectionString: null,
            inMemoryDatabaseName: null,
            useInMemoryFallback: false);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext());

        Assert.Contains("connection string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlServerFactory_AcceptsExplicitTenantConnectionString()
    {
        var factory = new BundleAppDbContextFactory(
            connectionString: null,
            inMemoryDatabaseName: null,
            useInMemoryFallback: false);

        using var context = factory.CreateDbContext(
            "Server=(localdb)\\MSSQLLocalDB;Database=AzuntBundleManagementTenantTest;Trusted_Connection=True;TrustServerCertificate=True");

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }
}
