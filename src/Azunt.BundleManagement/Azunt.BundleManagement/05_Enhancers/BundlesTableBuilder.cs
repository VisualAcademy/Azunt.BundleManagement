using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Azunt.BundleManagement;

/// <summary>
/// Creates the canonical dbo.Bundles table when it does not exist and safely
/// adds missing Bundle columns, defaults, and indexes to an existing table.
///
/// The caller supplies the connection string explicitly, which makes the
/// builder suitable for single-database and multi-tenant applications alike.
/// The class does not discover tenants or cache tenant connection strings.
/// </summary>
public sealed class BundlesTableBuilder
{
    private static readonly IReadOnlyList<ColumnDefinition> OptionalColumns =
    [
        new("Code", "NVARCHAR(100) NULL"),
        new("Version", "NVARCHAR(100) NULL"),
        new("Status", "NVARCHAR(50) NULL"),
        new("Description", "NVARCHAR(MAX) NULL"),
        new("CreatedBy", "NVARCHAR(255) NULL"),
        new("CreatedAt", "DATETIMEOFFSET(7) NULL"),
        new("ModifiedBy", "NVARCHAR(255) NULL"),
        new("ModifiedAt", "DATETIMEOFFSET(7) NULL")
    ];

    private readonly ILogger<BundlesTableBuilder> _logger;

    public BundlesTableBuilder(ILogger<BundlesTableBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ensures that the target database contains the Bundles schema required
    /// by Azunt.BundleManagement.
    /// </summary>
    /// <param name="connectionString">
    /// The database connection string to use. In a multi-tenant application,
    /// pass the current tenant's connection string here.
    /// </param>
    public async Task EnsureAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
        {
            await CreateTableAsync(connection, cancellationToken);
        }
        else
        {
            await ValidateRequiredColumnsAsync(connection, cancellationToken);

            foreach (var column in OptionalColumns)
            {
                await EnsureColumnAsync(connection, column, cancellationToken);
            }

            await EnsureIsActiveColumnAsync(connection, cancellationToken);
        }

        await EnsureDefaultConstraintAsync(
            connection,
            columnName: "IsActive",
            constraintName: "DF_Bundles_IsActive",
            defaultExpression: "(1)",
            cancellationToken: cancellationToken);

        await EnsureDefaultConstraintAsync(
            connection,
            columnName: "CreatedAt",
            constraintName: "DF_Bundles_CreatedAt",
            defaultExpression: "(SYSDATETIMEOFFSET())",
            cancellationToken: cancellationToken);

        await EnsureIndexAsync(connection, "IX_Bundles_Code", "Code", cancellationToken);
        await EnsureIndexAsync(connection, "IX_Bundles_Status", "Status", cancellationToken);
        await EnsureIndexAsync(connection, "IX_Bundles_IsActive", "IsActive", cancellationToken);

        _logger.LogInformation(
            "Bundles schema ensured for database {Database} on server {DataSource}.",
            connection.Database,
            connection.DataSource);
    }

    /// <summary>
    /// Applies the same schema ensure operation to an explicit set of database
    /// connection strings. Tenant discovery remains the responsibility of the
    /// host application.
    /// </summary>
    public async Task EnsureDatabasesAsync(
        IEnumerable<string> connectionStrings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionStrings);

        foreach (var connectionString in connectionStrings
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureAsync(connectionString, cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT_BIG(1)
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE s.name = N'dbo'
              AND t.name = N'Bundles';
            """;

        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private async Task CreateTableAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE [dbo].[Bundles]
            (
                [Id] INT IDENTITY(1,1) NOT NULL,
                [Name] NVARCHAR(255) NOT NULL,
                [Code] NVARCHAR(100) NULL,
                [Version] NVARCHAR(100) NULL,
                [Status] NVARCHAR(50) NULL,
                [Description] NVARCHAR(MAX) NULL,
                [IsActive] BIT NOT NULL CONSTRAINT [DF_Bundles_IsActive] DEFAULT(1),
                [CreatedBy] NVARCHAR(255) NULL,
                [CreatedAt] DATETIMEOFFSET(7) NULL CONSTRAINT [DF_Bundles_CreatedAt] DEFAULT(SYSDATETIMEOFFSET()),
                [ModifiedBy] NVARCHAR(255) NULL,
                [ModifiedAt] DATETIMEOFFSET(7) NULL,
                CONSTRAINT [PK_Bundles] PRIMARY KEY CLUSTERED ([Id] ASC)
            );
            """;

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation(
            "Created dbo.Bundles in database {Database}.",
            connection.Database);
    }

    private static async Task ValidateRequiredColumnsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, "Id", cancellationToken))
        {
            throw new InvalidOperationException(
                "dbo.Bundles exists but does not contain the required Id column. " +
                "The schema enhancer will not guess or replace an existing primary key.");
        }

        if (!await ColumnExistsAsync(connection, "Name", cancellationToken))
        {
            throw new InvalidOperationException(
                "dbo.Bundles exists but does not contain the required Name column. " +
                "Add or migrate the required business key column before using Azunt.BundleManagement.");
        }
    }

    private async Task EnsureColumnAsync(
        SqlConnection connection,
        ColumnDefinition column,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, column.Name, cancellationToken))
        {
            return;
        }

        var sql = $"ALTER TABLE [dbo].[Bundles] ADD [{column.Name}] {column.SqlDefinition};";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation(
            "Added column {ColumnName} to dbo.Bundles in database {Database}.",
            column.Name,
            connection.Database);
    }

    private async Task EnsureIsActiveColumnAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, "IsActive", cancellationToken))
        {
            const string addSql = """
                ALTER TABLE [dbo].[Bundles]
                ADD [IsActive] BIT NOT NULL
                    CONSTRAINT [DF_Bundles_IsActive] DEFAULT(1) WITH VALUES;
                """;

            await using var addCommand = new SqlCommand(addSql, connection);
            await addCommand.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Added IsActive to dbo.Bundles in database {Database}.",
                connection.Database);

            return;
        }

        const string metadataSql = """
            SELECT c.is_nullable, TYPE_NAME(c.user_type_id) AS DataType
            FROM sys.columns AS c
            WHERE c.object_id = OBJECT_ID(N'dbo.Bundles')
              AND c.name = N'IsActive';
            """;

        await using var metadataCommand = new SqlCommand(metadataSql, connection);
        await using var reader = await metadataCommand.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return;
        }

        var isNullable = reader.GetBoolean(0);
        var dataType = reader.GetString(1);
        await reader.DisposeAsync();

        if (!string.Equals(dataType, "bit", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"dbo.Bundles.IsActive must be BIT, but the existing type is {dataType}.");
        }

        if (!isNullable)
        {
            return;
        }

        const string normalizeSql = """
            UPDATE [dbo].[Bundles]
            SET [IsActive] = 1
            WHERE [IsActive] IS NULL;

            ALTER TABLE [dbo].[Bundles]
            ALTER COLUMN [IsActive] BIT NOT NULL;
            """;

        await using var normalizeCommand = new SqlCommand(normalizeSql, connection);
        await normalizeCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ColumnExistsAsync(
        SqlConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT_BIG(1)
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.Bundles')
              AND name = @ColumnName;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ColumnName", columnName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task EnsureDefaultConstraintAsync(
        SqlConnection connection,
        string columnName,
        string constraintName,
        string defaultExpression,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, columnName, cancellationToken))
        {
            return;
        }

        const string checkSql = """
            SELECT COUNT_BIG(1)
            FROM sys.default_constraints AS dc
            INNER JOIN sys.columns AS c
                ON c.object_id = dc.parent_object_id
               AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Bundles')
              AND c.name = @ColumnName;
            """;

        await using (var checkCommand = new SqlCommand(checkSql, connection))
        {
            checkCommand.Parameters.AddWithValue("@ColumnName", columnName);
            if (Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return;
            }
        }

        var addSql =
            $"ALTER TABLE [dbo].[Bundles] ADD CONSTRAINT [{constraintName}] " +
            $"DEFAULT {defaultExpression} FOR [{columnName}];";

        await using var addCommand = new SqlCommand(addSql, connection);
        await addCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureIndexAsync(
        SqlConnection connection,
        string indexName,
        string columnName,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, columnName, cancellationToken))
        {
            return;
        }

        const string checkSql = """
            SELECT COUNT_BIG(1)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.Bundles')
              AND name = @IndexName;
            """;

        await using (var checkCommand = new SqlCommand(checkSql, connection))
        {
            checkCommand.Parameters.AddWithValue("@IndexName", indexName);
            if (Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return;
            }
        }

        var createSql =
            $"CREATE NONCLUSTERED INDEX [{indexName}] " +
            $"ON [dbo].[Bundles] ([{columnName}] ASC);";

        await using var createCommand = new SqlCommand(createSql, connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ColumnDefinition(string Name, string SqlDefinition);
}
