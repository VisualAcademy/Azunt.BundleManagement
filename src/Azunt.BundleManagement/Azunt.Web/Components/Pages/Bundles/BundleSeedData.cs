using Azunt.BundleManagement;

namespace Azunt.Web.Components.Pages.Bundles;

public static class BundleSeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBundleRepository>();
        if ((await repository.GetAllAsync()).Count > 0) return;

        await repository.AddAsync(new Bundle
        {
            Name = "Standard Workstation Package",
            Code = "BND-WKS-001",
            Version = "1.0",
            Status = "Active",
            Description = "Generic example bundle for a standard workstation configuration.",
            IsActive = true,
            CreatedBy = "Sample User",
            CreatedAt = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.FromHours(9))
        });

        await repository.AddAsync(new Bundle
        {
            Name = "Field Service Kit",
            Code = "BND-FSK-002",
            Version = "2.1",
            Status = "Draft",
            Description = "Example reusable kit definition independent of any specific industry.",
            IsActive = true,
            CreatedBy = "Sample User",
            CreatedAt = new DateTimeOffset(2026, 8, 29, 15, 30, 0, TimeSpan.FromHours(-7))
        });

        await repository.AddAsync(new Bundle
        {
            Name = "Archived Configuration",
            Code = "BND-ARC-003",
            Version = "3.0",
            Status = "Retired",
            Description = "Inactive sample row for filter testing.",
            IsActive = false,
            CreatedBy = "Sample Import",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });
    }
}
