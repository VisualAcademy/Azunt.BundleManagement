using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace Azunt.BundleManagement;

public sealed class BundleRepositoryAdoNet : IBundleRepository
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BundleRepositoryAdoNet> _logger;

    public BundleRepositoryAdoNet(IConfiguration configuration, ILogger<BundleRepositoryAdoNet> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string ResolveConnectionString(string? connectionString)
        => !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

    public async Task<Bundle> AddAsync(Bundle model, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model);
        model.CreatedAt ??= DateTimeOffset.UtcNow;
        model.Status = NormalizeStatus(model.Status);

        const string sql = """
            INSERT INTO dbo.Bundles
                (Name, Code, Version, Status, Description, IsActive, CreatedBy, CreatedAt, ModifiedBy, ModifiedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@Name, @Code, @Version, @Status, @Description, @IsActive, @CreatedBy, @CreatedAt, @ModifiedBy, @ModifiedAt);
            """;

        await using var connection = new SqlConnection(ResolveConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, model, includeId: false);
        model.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
        _logger.LogInformation("Bundle {BundleId} ({BundleName}) created via ADO.NET.", model.Id, model.Name);
        return model;
    }

    public async Task<List<Bundle>> GetAllAsync(string? connectionString = null)
    {
        const string sql = "SELECT * FROM dbo.Bundles ORDER BY Id DESC;";
        await using var connection = new SqlConnection(ResolveConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        return await ReadManyAsync(reader);
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

        await using var connection = new SqlConnection(ResolveConnectionString(connectionString));
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Count", SqlDbType.Int).Value = take;

        await using var reader = await command.ExecuteReaderAsync();
        return await ReadManyAsync(reader);
    }

    public async Task<Bundle?> GetByIdAsync(int id, string? connectionString = null)
    {
        const string sql = "SELECT * FROM dbo.Bundles WHERE Id = @Id;";
        await using var connection = new SqlConnection(ResolveConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<bool> UpdateAsync(Bundle model, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model);
        model.ModifiedAt ??= DateTimeOffset.UtcNow;
        model.Status = NormalizeStatus(model.Status);

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

        await using var connection = new SqlConnection(ResolveConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, model, includeId: true);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id, string? connectionString = null)
    {
        const string sql = "DELETE FROM dbo.Bundles WHERE Id = @Id;";
        await using var connection = new SqlConnection(ResolveConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<PagedResult<Bundle>> GetPagedAsync(BundleFilterOptions options, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pageIndex = Math.Max(0, options.PageIndex);
        var pageSize = Math.Clamp(options.PageSize, 1, 200);
        var orderBy = ToOrderBy(options.SortOrder);

        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.SearchQuery))
        {
            filters.Add("(Name LIKE @Search OR Code LIKE @Search OR Version LIKE @Search OR Status LIKE @Search OR Description LIKE @Search OR CreatedBy LIKE @Search OR ModifiedBy LIKE @Search)");
        }
        if (!string.IsNullOrWhiteSpace(options.Status)) filters.Add("Status = @Status");
        if (options.ActiveOnly) filters.Add("IsActive = 1");
        var where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);

        await using var connection = new SqlConnection(ResolveConnectionString(connectionString));
        await connection.OpenAsync();

        long total;
        await using (var countCommand = new SqlCommand($"SELECT COUNT_BIG(1) FROM dbo.Bundles{where};", connection))
        {
            AddFilterParameters(countCommand, options);
            total = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
        }

        var sql = $"SELECT * FROM dbo.Bundles{where} ORDER BY {orderBy} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        await using var command = new SqlCommand(sql, connection);
        AddFilterParameters(command, options);
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = pageIndex * pageSize;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
        await using var reader = await command.ExecuteReaderAsync();
        var items = await ReadManyAsync(reader);
        return new PagedResult<Bundle>(items, total);
    }

    private static void AddFilterParameters(SqlCommand command, BundleFilterOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SearchQuery))
            command.Parameters.Add("@Search", SqlDbType.NVarChar, 4000).Value = $"%{options.SearchQuery.Trim()}%";
        if (!string.IsNullOrWhiteSpace(options.Status))
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = options.Status.Trim();
    }

    private static void AddParameters(SqlCommand command, Bundle model, bool includeId)
    {
        if (includeId) command.Parameters.Add("@Id", SqlDbType.Int).Value = model.Id;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = model.Name.Trim();
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 100).Value = DbValue(model.Code);
        command.Parameters.Add("@Version", SqlDbType.NVarChar, 100).Value = DbValue(model.Version);
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = DbValue(model.Status);
        command.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = DbValue(model.Description);
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = model.IsActive;
        command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 255).Value = DbValue(model.CreatedBy);
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = model.CreatedAt.HasValue ? model.CreatedAt.Value : DBNull.Value;
        command.Parameters.Add("@ModifiedBy", SqlDbType.NVarChar, 255).Value = DbValue(model.ModifiedBy);
        command.Parameters.Add("@ModifiedAt", SqlDbType.DateTimeOffset).Value = model.ModifiedAt.HasValue ? model.ModifiedAt.Value : DBNull.Value;
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static async Task<List<Bundle>> ReadManyAsync(SqlDataReader reader)
    {
        var result = new List<Bundle>();
        while (await reader.ReadAsync()) result.Add(Map(reader));
        return result;
    }

    private static Bundle Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        Code = GetNullableString(reader, "Code"),
        Version = GetNullableString(reader, "Version"),
        Status = GetNullableString(reader, "Status"),
        Description = GetNullableString(reader, "Description"),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        CreatedBy = GetNullableString(reader, "CreatedBy"),
        CreatedAt = GetNullableDateTimeOffset(reader, "CreatedAt"),
        ModifiedBy = GetNullableString(reader, "ModifiedBy"),
        ModifiedAt = GetNullableDateTimeOffset(reader, "ModifiedAt")
    };

    private static string? GetNullableString(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static string NormalizeStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Active" : value.Trim();

    private static void Validate(Bundle model)
    {
        if (string.IsNullOrWhiteSpace(model.Name)) throw new ValidationException("Bundle Name is required.");
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
