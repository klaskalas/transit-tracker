namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("route_shapes")]
public class RouteShape
{
    [Key]
    [Column("route_id", Order = 0)]
    public int RouteId { get; set; }

    [Key]
    [Column("gtfs_shape_id", Order = 1)]
    public string GtfsShapeId { get; set; } = null!;
}
