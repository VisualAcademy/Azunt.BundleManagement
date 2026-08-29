using Microsoft.EntityFrameworkCore.Storage;

namespace Azunt.BundleManagement;

public static class BundleInMemoryDatabase
{
    public const string DefaultName = "AzuntBundleManagement";
    public static readonly InMemoryDatabaseRoot Root = new();
}
