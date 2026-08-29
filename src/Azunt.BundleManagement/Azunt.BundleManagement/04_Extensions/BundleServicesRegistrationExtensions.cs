using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Azunt.BundleManagement;

public static class BundleServicesRegistrationExtensions
{
    public enum RepositoryMode
    {
        EfCoreInMemory,
        EfCoreSqlServer,
        Dapper,
        AdoNet
    }

    public static IServiceCollection AddDependencyInjectionContainerForBundleApp(
        this IServiceCollection services,
        RepositoryMode mode = RepositoryMode.EfCoreInMemory)
    {
        switch (mode)
        {
            case RepositoryMode.EfCoreInMemory:
                services.AddSingleton(new BundleAppDbContextFactory());
                services.AddScoped<IBundleRepository, BundleRepository>();
                break;

            case RepositoryMode.EfCoreSqlServer:
                services.AddSingleton<BundleAppDbContextFactory>(sp =>
                {
                    var configuration = sp.GetService<IConfiguration>();
                    var defaultConnectionString = configuration?.GetConnectionString("DefaultConnection");

                    // A default connection is optional. Multi-tenant applications can
                    // pass the current tenant connection string to each repository call.
                    return new BundleAppDbContextFactory(
                        defaultConnectionString,
                        inMemoryDatabaseName: null,
                        useInMemoryFallback: false);
                });
                services.AddScoped<IBundleRepository, BundleRepository>();
                break;

            case RepositoryMode.Dapper:
                services.AddScoped<IBundleRepository, BundleRepositoryDapper>();
                break;

            case RepositoryMode.AdoNet:
                services.AddScoped<IBundleRepository, BundleRepositoryAdoNet>();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        // The schema builder is independent of the selected repository mode and
        // always accepts an explicit connection string for tenant-aware usage.
        services.AddTransient<BundlesTableBuilder>();

        return services;
    }
}
