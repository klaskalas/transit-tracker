namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

[Table("shape_lines")]
public class ShapeLine
{
    [Key]
    [Column("gtfs_shape_id")]
    public string GtfsShapeId { get; set; } = null!;

    [Required]
    [Column("geom", TypeName = "geometry (LineString, 4326)")]
    public LineString Geom { get; set; } = null!;
}
