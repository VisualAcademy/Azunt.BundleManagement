using Microsoft.EntityFrameworkCore;

namespace Azunt.BundleManagement;

public class BundleAppDbContext : DbContext
{
    public BundleAppDbContext(DbContextOptions<BundleAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Bundle> Bundles => Set<Bundle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Bundle>();

        entity.ToTable("Bundles");
        entity.HasKey(m => m.Id);

        entity.Property(m => m.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();

        entity.Property(m => m.Name).HasMaxLength(255).IsRequired();
        entity.Property(m => m.Code).HasMaxLength(100);
        entity.Property(m => m.Version).HasMaxLength(100);
        entity.Property(m => m.Status).HasMaxLength(50);
        entity.Property(m => m.Description);
        entity.Property(m => m.IsActive).IsRequired();
        entity.Property(m => m.CreatedBy).HasMaxLength(255);
        entity.Property(m => m.CreatedAt).HasColumnType("datetimeoffset(7)");
        entity.Property(m => m.ModifiedBy).HasMaxLength(255);
        entity.Property(m => m.ModifiedAt).HasColumnType("datetimeoffset(7)");

        entity.HasIndex(m => m.Code).HasDatabaseName("IX_Bundles_Code");
        entity.HasIndex(m => m.Status).HasDatabaseName("IX_Bundles_Status");
        entity.HasIndex(m => m.IsActive).HasDatabaseName("IX_Bundles_IsActive");
    }
}
