using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace TransitTrackerWebApi.Models;

public class Route
{
    [Key]
    public Guid Id { get; set; }

    // GTFS shape_id (unik identifierare för ruttens form)
    [Required]
    public string ShapeId { get; set; } = string.Empty;

    // Valfritt namn
    public string Name { get; set; } = string.Empty;

    // GeoJSON som text, t.ex. för frontend-bruk
    [Column(TypeName = "jsonb")]
    public string GeoJsonShape { get; set; } = string.Empty;

    // Geometrin som PostGIS LineString (används av spatial queries)
    public LineString? Geom { get; set; }
}