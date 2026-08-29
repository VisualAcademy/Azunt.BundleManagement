using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Azunt.BundleManagement;

public sealed class BundleRepository : IBundleRepository
{
    private readonly BundleAppDbContextFactory _factory;
    private readonly ILogger<BundleRepository> _logger;

    public BundleRepository(BundleAppDbContextFactory factory, ILogger<BundleRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    private BundleAppDbContext CreateContext(string? connectionString)
        => _factory.CreateDbContext(connectionString);

    public async Task<Bundle> AddAsync(Bundle model, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model);

        await using var context = CreateContext(connectionString);
        model.CreatedAt ??= DateTimeOffset.UtcNow;
        model.Status = NormalizeStatus(model.Status);

        context.Bundles.Add(model);
        await context.SaveChangesAsync();

        _logger.LogInformation("Bundle {BundleId} ({BundleName}) created.", model.Id, model.Name);
        return model;
    }

    public async Task<List<Bundle>> GetAllAsync(string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);
        return await context.Bundles.AsNoTracking().OrderByDescending(m => m.Id).ToListAsync();
    }

    public async Task<Bundle?> GetByIdAsync(int id, string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);
        return await context.Bundles.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
    }

    public async Task<bool> UpdateAsync(Bundle model, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model);

        await using var context = CreateContext(connectionString);
        var entity = await context.Bundles.SingleOrDefaultAsync(m => m.Id == model.Id);
        if (entity is null)
        {
            return false;
        }

        entity.Name = model.Name.Trim();
        entity.Code = TrimOrNull(model.Code);
        entity.Version = TrimOrNull(model.Version);
        entity.Status = NormalizeStatus(model.Status);
        entity.Description = TrimOrNull(model.Description);
        entity.IsActive = model.IsActive;
        entity.ModifiedBy = TrimOrNull(model.ModifiedBy);
        entity.ModifiedAt = model.ModifiedAt ?? DateTimeOffset.UtcNow;

        var changed = await context.SaveChangesAsync() > 0;
        if (changed)
        {
            _logger.LogInformation("Bundle {BundleId} updated by {ModifiedBy}.", entity.Id, entity.ModifiedBy);
        }

        return changed;
    }

    public async Task<bool> DeleteAsync(int id, string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);
        var entity = await context.Bundles.SingleOrDefaultAsync(m => m.Id == id);
        if (entity is null)
        {
            return false;
        }

        context.Bundles.Remove(entity);
        var changed = await context.SaveChangesAsync() > 0;
        if (changed)
        {
            _logger.LogInformation("Bundle {BundleId} deleted.", id);
        }

        return changed;
    }

    public async Task<PagedResult<Bundle>> GetPagedAsync(
        BundleFilterOptions options,
        string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using var context = CreateContext(connectionString);
        IQueryable<Bundle> query = context.Bundles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(options.SearchQuery))
        {
            var keyword = options.SearchQuery.Trim();
            query = query.Where(m =>
                m.Name.Contains(keyword) ||
                (m.Code != null && m.Code.Contains(keyword)) ||
                (m.Version != null && m.Version.Contains(keyword)) ||
                (m.Status != null && m.Status.Contains(keyword)) ||
                (m.Description != null && m.Description.Contains(keyword)) ||
                (m.CreatedBy != null && m.CreatedBy.Contains(keyword)) ||
                (m.ModifiedBy != null && m.ModifiedBy.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(options.Status))
        {
            var status = options.Status.Trim();
            query = query.Where(m => m.Status == status);
        }

        if (options.ActiveOnly)
        {
            query = query.Where(m => m.IsActive);
        }

        query = ApplySort(query, options.SortOrder);

        var totalCount = await query.LongCountAsync();
        var pageIndex = Math.Max(0, options.PageIndex);
        var pageSize = Math.Clamp(options.PageSize, 1, 200);
        var items = await query.Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<Bundle>(items, totalCount);
    }

    private static IQueryable<Bundle> ApplySort(IQueryable<Bundle> query, string? sortOrder)
        => sortOrder switch
        {
            "Name" => query.OrderBy(m => m.Name).ThenBy(m => m.Id),
            "NameDesc" => query.OrderByDescending(m => m.Name).ThenByDescending(m => m.Id),
            "Code" => query.OrderBy(m => m.Code).ThenBy(m => m.Name),
            "CodeDesc" => query.OrderByDescending(m => m.Code).ThenByDescending(m => m.Name),
            "Version" => query.OrderBy(m => m.Version).ThenBy(m => m.Name),
            "VersionDesc" => query.OrderByDescending(m => m.Version).ThenByDescending(m => m.Name),
            "Status" => query.OrderBy(m => m.Status).ThenBy(m => m.Name),
            "StatusDesc" => query.OrderByDescending(m => m.Status).ThenByDescending(m => m.Name),
            "CreatedAt" => query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id),
            "CreatedAtDesc" => query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id),
            "ModifiedAt" => query.OrderBy(m => m.ModifiedAt).ThenBy(m => m.Id),
            "ModifiedAtDesc" => query.OrderByDescending(m => m.ModifiedAt).ThenByDescending(m => m.Id),
            _ => query.OrderByDescending(m => m.Id)
        };

    private static void Validate(Bundle model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new ValidationException("Bundle Name is required.");
        }

        if (model.Name.Length > 255)
        {
            throw new ValidationException("Bundle Name must be 255 characters or fewer.");
        }
    }

    private static string NormalizeStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Active" : value.Trim();

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
