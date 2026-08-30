using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.ComponentModel.DataAnnotations;

namespace Azunt.BundleManagement;

public sealed class BundleRepositoryDapper : IBundleRepository
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BundleRepositoryDapper> _logger;

    public BundleRepositoryDapper(IConfiguration configuration, ILogger<BundleRepositoryDapper> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string ResolveConnectionString(string? connectionString)
        => !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

    private SqlConnection CreateConnection(string? connectionString)
        => new(ResolveConnectionString(connectionString));

    public async Task<Bundle> AddAsync(Bundle model, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(model.Name)) throw new ValidationException("Bundle Name is required.");

        model.CreatedAt ??= DateTimeOffset.UtcNow;
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status.Trim();

        const string sql = """
            INSERT INTO dbo.Bundles
                (Name, Code, Version, Status, Description, IsActive, CreatedBy, CreatedAt, ModifiedBy, ModifiedAt)
            VALUES
                (@Name, @Code, @Version, @Status, @Description, @IsActive, @CreatedBy, @CreatedAt, @ModifiedBy, @ModifiedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        await using var connection = CreateConnection(connectionString);
        model.Id = await connection.ExecuteScalarAsync<int>(sql, model);
        _logger.LogInformation("Bundle {BundleId} ({BundleName}) created via Dapper.", model.Id, model.Name);
        return model;
    }

    public async Task<List<Bundle>> GetAllAsync(string? connectionString = null)
    {
        const string sql = "SELECT * FROM dbo.Bundles ORDER BY Id DESC;";
        await using var connection = CreateConnection(connectionString);
        return (await connection.QueryAsync<Bundle>(sql)).AsList();
    }

    public async Task<List<Bundle>> GetRecentAsync(
        int count = 5,
        string? connectionString = null)
    {
        var take = Math.Clamp(count, 1, 50);

        const string sql = """
            SELECT TOP (@Count) *
            FROM dbo.Bundles
            ORDER BY COALESCE(ModifiedAt, CreatedAt) DESC, Id DESC;
            """;

        await using var connection = CreateConnection(connectionString);
        return (await connection.QueryAsync<Bundle>(
            sql,
            new { Count = take }))
            .AsList();
    }

    public async Task<Bundle?> GetByIdAsync(int id, string? connectionString = null)
    {
        const string sql = "SELECT * FROM dbo.Bundles WHERE Id = @Id;";
        await using var connection = CreateConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<Bundle>(sql, new { Id = id });
    }

    public async Task<bool> UpdateAsync(Bundle model, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(model.Name)) throw new ValidationException("Bundle Name is required.");
        model.ModifiedAt ??= DateTimeOffset.UtcNow;
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status.Trim();

        const string sql = """
            UPDATE dbo.Bundles
            SET Name = @Name,
                Code = @Code,
                Version = @Version,
                Status = @Status,
                Description = @Description,
                IsActive = @IsActive,
                ModifiedBy = @ModifiedBy,
                ModifiedAt = @ModifiedAt
            WHERE Id = @Id;
            """;

        await using var connection = CreateConnection(connectionString);
        return await connection.ExecuteAsync(sql, model) > 0;
    }

    public async Task<bool> DeleteAsync(int id, string? connectionString = null)
    {
        const string sql = "DELETE FROM dbo.Bundles WHERE Id = @Id;";
        await using var connection = CreateConnection(connectionString);
        return await connection.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<PagedResult<Bundle>> GetPagedAsync(BundleFilterOptions options, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pageIndex = Math.Max(0, options.PageIndex);
        var pageSize = Math.Clamp(options.PageSize, 1, 200);
        var orderBy = ToOrderBy(options.SortOrder);

        var where = new List<string>();
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(options.SearchQuery))
        {
            where.Add("(Name LIKE @Search OR Code LIKE @Search OR Version LIKE @Search OR Status LIKE @Search OR Description LIKE @Search OR CreatedBy LIKE @Search OR ModifiedBy LIKE @Search)");
            parameters.Add("Search", $"%{options.SearchQuery.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(options.Status))
        {
            where.Add("Status = @Status");
            parameters.Add("Status", options.Status.Trim());
        }
        if (options.ActiveOnly)
        {
            where.Add("IsActive = 1");
        }

        var whereSql = where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where);
        parameters.Add("Offset", pageIndex * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $"""
            SELECT COUNT_BIG(1) FROM dbo.Bundles{whereSql};
            SELECT * FROM dbo.Bundles{whereSql}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var connection = CreateConnection(connectionString);
        using var multi = await connection.QueryMultipleAsync(sql, parameters);
        var total = await multi.ReadSingleAsync<long>();
        var items = (await multi.ReadAsync<Bundle>()).AsList();
        return new PagedResult<Bundle>(items, total);
    }

    private static string ToOrderBy(string? sortOrder) => sortOrder switch
    {
        "Name" => "Name ASC, Id ASC",
        "NameDesc" => "Name DESC, Id DESC",
        "Code" => "Code ASC, Name ASC",
        "CodeDesc" => "Code DESC, Name DESC",
        "Version" => "Version ASC, Name ASC",
        "VersionDesc" => "Version DESC, Name DESC",
        "Status" => "Status ASC, Name ASC",
        "StatusDesc" => "Status DESC, Name DESC",
        "CreatedAt" => "CreatedAt ASC, Id ASC",
        "CreatedAtDesc" => "CreatedAt DESC, Id DESC",
        "ModifiedAt" => "ModifiedAt ASC, Id ASC",
        "ModifiedAtDesc" => "ModifiedAt DESC, Id DESC",
        _ => "Id DESC"
    };
}
