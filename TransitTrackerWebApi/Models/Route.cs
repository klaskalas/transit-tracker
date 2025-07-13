namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("routes")]
public class Route
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey("Agency")]
    [Column("agency_id")]
    public int AgencyId { get; set; }

    public Agency? Agency { get; set; }

    [Required]
    [Column("gtfs_route_id")]
    public string GtfsRouteId { get; set; } = null!;

    [Column("short_name")]
    public string? ShortName { get; set; }

    [Column("long_name")]
    public string? LongName { get; set; }

    [Column("route_type")]
    public int RouteType { get; set; }

    [Column("color")]
    public string? Color { get; set; }

    [Column("text_color")]
    public string? TextColor { get; set; }
}
