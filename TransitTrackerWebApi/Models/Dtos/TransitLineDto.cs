namespace TransitTrackerWebApi.Models.Dtos;

public class TransitLineDto
{
    public int Id { get; set; }
    public int AgencyId { get; set; }
    public string AgencyName { get; set; }
    public string GtfsRouteId { get; set; }
    public string? ShortName { get; set; }
    public string? LongName { get; set; }
    public int RouteType { get; set; }
    public string? Color { get; set; }
    public string? TextColor { get; set; }
}