# Azunt.BundleManagement

`Azunt.BundleManagement` is a standalone .NET 8 module for managing reusable **Bundle** definitions in inventory, catalog, configuration, kit, package, software-set, and other business applications.

The repository is designed as an independent GitHub/NuGet project with no dependency on a host application's domain model. The core library owns only the `Bundles` aggregate and its CRUD boundary.

## Solution structure

```text
Azunt.BundleManagement-main/
├─ .github/
│  └─ workflows/
│     └─ dotnet.yml
├─ CHANGELOG.md
├─ LICENSE
├─ README.md
└─ src/
   └─ Azunt.BundleManagement/
      ├─ Azunt.BundleManagement.sln
      ├─ Azunt.BundleManagement/        # NuGet-ready class library
      ├─ Azunt.SqlServer/               # Canonical SQL Server database project
      ├─ Azunt.Web/                     # Blazor Web App CRUD demo
      └─ Azunt.BundleManagement.Tests/  # xUnit tests
```

The reusable `Azunt.BundleManagement` class library and its xUnit tests target **.NET 8** for broad compatibility, while the `Azunt.Web` Blazor CRUD demo targets **.NET 10**. A .NET 10 application can reference the .NET 8 class library directly.

## Bundle model

The canonical Bundle model contains:

| Property | Type | Purpose |
|---|---|---|
| `Id` | `int` | Primary key |
| `Name` | `string` | Required bundle name |
| `Code` | `string?` | Optional business code |
| `Version` | `string?` | Optional version identifier |
| `Status` | `string?` | Lifecycle status |
| `Description` | `string?` | Notes or description |
| `IsActive` | `bool` | Active/inactive flag |
| `CreatedBy` | `string?` | Creator identifier |
| `CreatedAt` | `DateTimeOffset?` | Offset-aware creation timestamp |
| `ModifiedBy` | `string?` | Last editor identifier |
| `ModifiedAt` | `DateTimeOffset?` | Offset-aware modification timestamp |

### Time-zone-aware timestamps

`CreatedAt` and `ModifiedAt` use `DateTimeOffset?` and map to SQL Server `DATETIMEOFFSET(7)`.

This preserves both the instant and the UTC offset. The sample `Azunt.Web` application reads the browser UTC offset and uses it when creating or updating records. For example, a browser in Korea can store `+09:00`, while a browser in another region can store that user's offset.

An offset is not the same as a named time zone. If an application must retain rules such as daylight-saving transitions, store a separate IANA/Windows time-zone identifier at the application layer.

## Class library

Project:

```text
src/Azunt.BundleManagement/Azunt.BundleManagement
```

The NuGet-ready library provides:

- `Bundle`
- `BundleFilterOptions`
- `PagedResult<T>`
- `IBundleRepository`
- `BundleRepository` for EF Core
- `BundleRepositoryDapper` for Dapper
- `BundleRepositoryAdoNet` for ADO.NET
- `BundleAppDbContext`
- `BundleAppDbContextFactory`
- `BundlesTableBuilder` for create/expand schema setup
- dependency-injection registration extensions

### Repository modes

The same `IBundleRepository` contract can be registered with one of four modes:

- `EfCoreInMemory`
- `EfCoreSqlServer`
- `Dapper`
- `AdoNet`

### EF Core In-Memory

```csharp
builder.Services.AddDependencyInjectionContainerForBundleApp(
    BundleServicesRegistrationExtensions.RepositoryMode.EfCoreInMemory);
```

This is the default mode used by `Azunt.Web` and requires no SQL Server instance.

### EF Core SQL Server

```csharp
builder.Services.AddDependencyInjectionContainerForBundleApp(
    BundleServicesRegistrationExtensions.RepositoryMode.EfCoreSqlServer);
```

Configure a connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### Dapper

```csharp
builder.Services.AddDependencyInjectionContainerForBundleApp(
    BundleServicesRegistrationExtensions.RepositoryMode.Dapper);
```

### ADO.NET

```csharp
builder.Services.AddDependencyInjectionContainerForBundleApp(
    BundleServicesRegistrationExtensions.RepositoryMode.AdoNet);
```

Dapper and ADO.NET use the same `DefaultConnection` setting unless an explicit connection string is passed to a repository method.


## Schema creation and enhancement

The NuGet package includes `BundlesTableBuilder` under `05_Enhancers`.

It accepts an explicit SQL Server connection string and performs an idempotent schema ensure:

- creates `dbo.Bundles` when the table does not exist
- validates the required `Id` and `Name` columns when the table already exists
- adds missing optional Bundle columns without dropping existing data
- normalizes a nullable `IsActive` column to `BIT NOT NULL`
- ensures the `CreatedAt` and `IsActive` defaults
- ensures indexes for `Code`, `Status`, and `IsActive`

```csharp
var tableBuilder = serviceProvider.GetRequiredService<BundlesTableBuilder>();

await tableBuilder.EnsureAsync(connectionString);
```

The enhancer intentionally does **not** discover application tenants, rename legacy columns, drop columns, or guess the time-zone meaning of old `DATETIME` values. A host application remains responsible for any domain-specific migration.

For provisioning several known databases, an explicit collection can also be supplied:

```csharp
await tableBuilder.EnsureDatabasesAsync(tenantConnectionStrings);
```

Schema ensure is normally best run during tenant provisioning, deployment/admin setup, or a controlled lazy-initialization step rather than before every CRUD request.

## Multi-tenant usage

All repository methods already accept an optional connection string. This allows the host application to resolve the current tenant database and pass that connection directly to both schema setup and CRUD operations.

```csharp
var tenantConnectionString = currentTenant.DatabaseConnectionString;

await tableBuilder.EnsureAsync(tenantConnectionString);

var page = await repository.GetPagedAsync(
    new BundleFilterOptions
    {
        PageIndex = 0,
        PageSize = 20
    },
    tenantConnectionString);
```

The package does not cache the tenant connection string and does not depend on a specific tenant registry, authentication model, or host application.

When `EfCoreSqlServer` mode is registered, `DefaultConnection` is optional. If it is absent, SQL Server repository operations must receive an explicit connection string. This prevents a multi-tenant application from being forced to configure a single global database.

## SQL Server database project

Project:

```text
src/Azunt.BundleManagement/Azunt.SqlServer
```

The database project contains only the canonical `dbo.Bundles` schema required by this module.

```sql
CREATE TABLE [dbo].[Bundles]
(
    [Id]          INT IDENTITY(1,1) NOT NULL,
    [Name]        NVARCHAR(255) NOT NULL,
    [Code]        NVARCHAR(100) NULL,
    [Version]     NVARCHAR(100) NULL,
    [Status]      NVARCHAR(50) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [IsActive]    BIT NOT NULL,
    [CreatedBy]   NVARCHAR(255) NULL,
    [CreatedAt]   DATETIMEOFFSET(7) NULL,
    [ModifiedBy]  NVARCHAR(255) NULL,
    [ModifiedAt]  DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_Bundles] PRIMARY KEY CLUSTERED ([Id] ASC)
);
```

Indexes are included for `Code`, `Status`, and `IsActive`.

The database project represents the canonical schema for a new installation of this module.

## Azunt.Web CRUD demo

Project:

```text
src/Azunt.BundleManagement/Azunt.Web
```

The sample Blazor Web App targets **.NET 10** and runs with EF Core In-Memory by default. Start the project and open:

```text
/Bundles
```

The page supports:

- browser language/region-aware date display
- Create
- Read/list
- Update
- Delete
- Search
- Status filter
- Active-only filter
- Sorting
- Paging
- browser UTC-offset detection
- browser language/region detection for user-friendly date formatting
- offset-aware create/update timestamps

The sample application also exposes REST endpoints:

| Method | URL | Purpose |
|---|---|---|
| `GET` | `/api/bundles` | Search/filter/sort/page |
| `GET` | `/api/bundles/{id}` | Read one record |
| `POST` | `/api/bundles` | Create |
| `PUT` | `/api/bundles/{id}` | Update |
| `DELETE` | `/api/bundles/{id}` | Delete |

The seed data is generic and exists only to make local CRUD testing immediate.

## Automated tests

Project:

```text
src/Azunt.BundleManagement/Azunt.BundleManagement.Tests
```

The xUnit tests use isolated EF Core In-Memory databases and cover:

- create defaults
- `DateTimeOffset` offset preservation
- read by ID
- update
- creation audit preservation during update
- delete
- search
- status filtering
- active filtering
- sorting
- paging
- schema builder connection-string validation
- SQL Server factory behavior for explicit tenant connections

## Build and run

Install the **.NET 8 SDK** for the reusable library/tests and the **.NET 10 SDK** for `Azunt.Web`. From the repository root:

```bash
cd src/Azunt.BundleManagement

dotnet restore Azunt.BundleManagement/Azunt.BundleManagement.csproj
dotnet restore Azunt.Web/Azunt.Web.csproj
dotnet restore Azunt.BundleManagement.Tests/Azunt.BundleManagement.Tests.csproj

dotnet build Azunt.BundleManagement/Azunt.BundleManagement.csproj
dotnet build Azunt.Web/Azunt.Web.csproj
dotnet test Azunt.BundleManagement.Tests/Azunt.BundleManagement.Tests.csproj

dotnet run --project Azunt.Web/Azunt.Web.csproj
```

The SSDT-style `Azunt.SqlServer` project can be opened and built with Visual Studio and SQL Server Data Tools.

## NuGet package

Create the package with:

```bash
dotnet pack Azunt.BundleManagement/Azunt.BundleManagement.csproj -c Release
```

Package ID:

```text
Azunt.BundleManagement
```

The library is intentionally limited to general Bundle management so consuming applications can define their own relationships and workflows independently.
