# Changelog

## 1.0.0 - 2026-08-30

- Added the initial standalone `Azunt.BundleManagement` .NET 8 library.
- Added a canonical Bundle model and `IBundleRepository` contract.
- Added EF Core In-Memory and SQL Server repository support.
- Added Dapper and ADO.NET repository implementations.
- Added offset-aware `DateTimeOffset` audit timestamps mapped to SQL Server `DATETIMEOFFSET(7)`.
- Added a canonical SQL Server database project for `dbo.Bundles`.
- Added `BundlesTableBuilder` for idempotent table creation and safe schema enhancement using an explicit database connection string.
- Added multi-tenant-friendly SQL Server registration so a host can pass the current tenant connection string per repository/schema call without requiring a global `DefaultConnection`.
- Added a .NET 10 Blazor Web App for CRUD verification while keeping the reusable library on .NET 8.
- Added browser language/region-aware date display in the Blazor demo while preserving `DateTimeOffset` values in storage.
- Added REST API endpoints for Bundle CRUD operations.
- Added xUnit tests for CRUD, filtering, sorting, paging, and timestamp offset preservation.
- Added GitHub Actions for restore, build, and test validation.
- Included the required Blazor `RenderMode` import for `@rendermode InteractiveServer`.
