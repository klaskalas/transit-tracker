namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("feeds")]
public class Feed
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey("Region")]
    [Column("region_id")]
    public int RegionId { get; set; }

    public Region? Region { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = null!;

    [Required]
    [Column("source_url")]
    public string SourceUrl { get; set; } = null!;

    [Column("version")]
    public string? Version { get; set; }

    [Column("imported_at")]
    public DateTimeOffset? ImportedAt { get; set; }

    [Column("scope")]
    public DataScope Scope { get; set; } = DataScope.Regional;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    public ICollection<Route> Routes { get; set; } = new List<Route>();
}
