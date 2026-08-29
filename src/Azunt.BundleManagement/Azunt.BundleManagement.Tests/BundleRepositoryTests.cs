using Azunt.BundleManagement;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Azunt.BundleManagement.Tests;

public class BundleRepositoryTests
{
    private static BundleRepository CreateRepository(string databaseName)
    {
        var factory = new BundleAppDbContextFactory(null, $"{databaseName}-{Guid.NewGuid():N}");
        return new BundleRepository(factory, NullLogger<BundleRepository>.Instance);
    }

    [Fact]
    public async Task AddAsync_CreatesBundleAndDefaultsTimestamp()
    {
        var repository = CreateRepository(nameof(AddAsync_CreatesBundleAndDefaultsTimestamp));
        var created = await repository.AddAsync(new Bundle { Name = "Core Package", CreatedBy = "Tester" });

        Assert.True(created.Id > 0);
        Assert.Equal("Active", created.Status);
        Assert.True(created.IsActive);
        Assert.NotNull(created.CreatedAt);
    }

    [Fact]
    public async Task AddAsync_PreservesProvidedOffset()
    {
        var repository = CreateRepository(nameof(AddAsync_PreservesProvidedOffset));
        var timestamp = new DateTimeOffset(2026, 8, 30, 10, 15, 0, TimeSpan.FromHours(9));

        var created = await repository.AddAsync(new Bundle { Name = "Seoul Package", CreatedAt = timestamp });
        var loaded = await repository.GetByIdAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal(TimeSpan.FromHours(9), loaded!.CreatedAt!.Value.Offset);
        Assert.Equal(timestamp, loaded.CreatedAt.Value);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsExpectedBundle()
    {
        var repository = CreateRepository(nameof(GetByIdAsync_ReturnsExpectedBundle));
        var created = await repository.AddAsync(new Bundle { Name = "Lookup", Code = "B-100" });

        var loaded = await repository.GetByIdAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Lookup", loaded!.Name);
        Assert.Equal("B-100", loaded.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsAndPreservesCreationAudit()
    {
        var repository = CreateRepository(nameof(UpdateAsync_UpdatesFieldsAndPreservesCreationAudit));
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5));
        var created = await repository.AddAsync(new Bundle
        {
            Name = "Before",
            CreatedBy = "Creator",
            CreatedAt = createdAt
        });

        created.Name = "After";
        created.Status = "Retired";
        created.ModifiedBy = "Editor";
        created.ModifiedAt = new DateTimeOffset(2026, 8, 30, 18, 0, 0, TimeSpan.FromHours(2));

        Assert.True(await repository.UpdateAsync(created));
        var loaded = await repository.GetByIdAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal("After", loaded!.Name);
        Assert.Equal("Retired", loaded.Status);
        Assert.Equal("Creator", loaded.CreatedBy);
        Assert.Equal(createdAt, loaded.CreatedAt);
        Assert.Equal("Editor", loaded.ModifiedBy);
        Assert.Equal(TimeSpan.FromHours(2), loaded.ModifiedAt!.Value.Offset);
    }

    [Fact]
    public async Task DeleteAsync_DeletesBundle()
    {
        var repository = CreateRepository(nameof(DeleteAsync_DeletesBundle));
        var created = await repository.AddAsync(new Bundle { Name = "Delete Me" });

        Assert.True(await repository.DeleteAsync(created.Id));
        Assert.Null(await repository.GetByIdAsync(created.Id));
        Assert.False(await repository.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task GetPagedAsync_FiltersSearchStatusAndActive()
    {
        var repository = CreateRepository(nameof(GetPagedAsync_FiltersSearchStatusAndActive));
        await repository.AddAsync(new Bundle { Name = "Alpha Kit", Code = "A1", Status = "Active", IsActive = true });
        await repository.AddAsync(new Bundle { Name = "Beta Kit", Code = "B1", Status = "Draft", IsActive = true });
        await repository.AddAsync(new Bundle { Name = "Alpha Archived", Code = "A2", Status = "Active", IsActive = false });

        var result = await repository.GetPagedAsync(new BundleFilterOptions
        {
            SearchQuery = "Alpha",
            Status = "Active",
            ActiveOnly = true,
            PageSize = 10
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Alpha Kit", result.Items[0].Name);
    }

    [Fact]
    public async Task GetPagedAsync_SortsAndPages()
    {
        var repository = CreateRepository(nameof(GetPagedAsync_SortsAndPages));
        await repository.AddAsync(new Bundle { Name = "Charlie" });
        await repository.AddAsync(new Bundle { Name = "Alpha" });
        await repository.AddAsync(new Bundle { Name = "Bravo" });

        var page0 = await repository.GetPagedAsync(new BundleFilterOptions
        {
            SortOrder = "Name",
            PageIndex = 0,
            PageSize = 2
        });
        var page1 = await repository.GetPagedAsync(new BundleFilterOptions
        {
            SortOrder = "Name",
            PageIndex = 1,
            PageSize = 2
        });

        Assert.Equal(3, page0.TotalCount);
        Assert.Equal(["Alpha", "Bravo"], page0.Items.Select(x => x.Name).ToArray());
        Assert.Single(page1.Items);
        Assert.Equal("Charlie", page1.Items[0].Name);
    }
}
