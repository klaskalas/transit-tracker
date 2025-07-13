
namespace TransitTrackerWebApi.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

[Table("shapes")]
public class Shape
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("gtfs_shape_id")]
    public string GtfsShapeId { get; set; } = null!;

    [Required]
    [Column("sequence")]
    public int Sequence { get; set; }

    [Required]
    [Column("lat")]
    public double Lat { get; set; }

    [Required]
    [Column("lon")]
    public double Lon { get; set; }

    [Column("geom", TypeName = "geometry (Point, 4326)")]
    public Point? Geom { get; set; }
}
