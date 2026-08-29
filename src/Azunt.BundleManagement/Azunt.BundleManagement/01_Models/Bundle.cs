using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Azunt.BundleManagement;

/// <summary>
/// Represents a reusable bundle definition with lifecycle and audit metadata.
/// </summary>
[Table("Bundles")]
public class Bundle
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Code { get; set; }

    [StringLength(100)]
    public string? Version { get; set; }

    [StringLength(50)]
    public string? Status { get; set; } = "Active";

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(255)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    [StringLength(255)]
    public string? ModifiedBy { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }
}
