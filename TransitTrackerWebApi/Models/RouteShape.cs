namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations.Schema;

[Table("route_shapes")]
public class RouteShape
{
    [Column("route_id", Order = 0)]
    public int RouteId { get; set; }

    [Column("gtfs_shape_id", Order = 1)]
    public string GtfsShapeId { get; set; } = null!;
}
