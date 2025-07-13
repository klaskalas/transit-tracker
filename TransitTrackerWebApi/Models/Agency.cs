namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("agencies")]
public class Agency
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

    [Column("agency_url")]
    public string? AgencyUrl { get; set; }

    [Column("timezone")]
    public string? Timezone { get; set; }
}
