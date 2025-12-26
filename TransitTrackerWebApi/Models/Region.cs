namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("regions")]
public class Region
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = null!;

    [Required]
    [Column("country_code")]
    public string CountryCode { get; set; } = null!;

    [Column("scope")]
    public DataScope Scope { get; set; } = DataScope.Regional;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("min_lat")]
    public double? MinLat { get; set; }

    [Column("min_lon")]
    public double? MinLon { get; set; }

    [Column("max_lat")]
    public double? MaxLat { get; set; }

    [Column("max_lon")]
    public double? MaxLon { get; set; }

    public ICollection<Feed> Feeds { get; set; } = new List<Feed>();
}
