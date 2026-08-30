namespace Azunt.BundleManagement;

public interface IBundleRepository
{
    Task<Bundle> AddAsync(Bundle model, string? connectionString = null);
    Task<List<Bundle>> GetAllAsync(string? connectionString = null);
    Task<List<Bundle>> GetRecentAsync(int count = 5, string? connectionString = null);
    Task<Bundle?> GetByIdAsync(int id, string? connectionString = null);
    Task<bool> UpdateAsync(Bundle model, string? connectionString = null);
    Task<bool> DeleteAsync(int id, string? connectionString = null);
    Task<PagedResult<Bundle>> GetPagedAsync(BundleFilterOptions options, string? connectionString = null);
}
