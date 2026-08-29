using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Azunt.BundleManagement;

public class BundleAppDbContextFactory
{
    private readonly IConfiguration? _configuration;
    private readonly string? _defaultConnectionString;
    private readonly string? _inMemoryDatabaseName;
    private readonly bool _useInMemoryFallback;

    public BundleAppDbContextFactory()
    {
        _useInMemoryFallback = true;
    }

    public BundleAppDbContextFactory(string defaultConnectionString)
    {
        _defaultConnectionString = defaultConnectionString;
        _useInMemoryFallback = false;
    }

    public BundleAppDbContextFactory(
        string? connectionString,
        string? inMemoryDatabaseName,
        bool useInMemoryFallback = true)
    {
        _defaultConnectionString = connectionString;
        _inMemoryDatabaseName = inMemoryDatabaseName;
        _useInMemoryFallback = useInMemoryFallback;
    }

    public BundleAppDbContextFactory(IConfiguration configuration)
    {
        _configuration = configuration;
        _useInMemoryFallback = true;
    }

    public BundleAppDbContext CreateDbContext()
    {
        if (!string.IsNullOrWhiteSpace(_defaultConnectionString))
        {
            return CreateSqlServerDbContext(_defaultConnectionString);
        }

        var configuredConnection = _configuration?.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            return CreateSqlServerDbContext(configuredConnection);
        }

        if (!_useInMemoryFallback)
        {
            throw new InvalidOperationException(
                "A SQL Server connection string is required. " +
                "Configure DefaultConnection or pass the current tenant connection string " +
                "to the repository method.");
        }

        return CreateInMemoryDbContext(
            _inMemoryDatabaseName ?? BundleInMemoryDatabase.DefaultName);
    }

    public BundleAppDbContext CreateDbContext(string? connectionString)
    {
        return string.IsNullOrWhiteSpace(connectionString)
            ? CreateDbContext()
            : CreateSqlServerDbContext(connectionString);
    }

    public BundleAppDbContext CreateInMemoryDbContext(
        string databaseName = BundleInMemoryDatabase.DefaultName)
    {
        var options = new DbContextOptionsBuilder<BundleAppDbContext>()
            .UseInMemoryDatabase(databaseName, BundleInMemoryDatabase.Root)
            .Options;

        return new BundleAppDbContext(options);
    }

    public BundleAppDbContext CreateSqlServerDbContext(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        var options = new DbContextOptionsBuilder<BundleAppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new BundleAppDbContext(options);
    }
}
