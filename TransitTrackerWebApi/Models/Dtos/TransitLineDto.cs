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
    public int? StopCount { get; set; }
    public double? LongestTripLengthMeters { get; set; }
}

public class TransitLineProgressDto
{
    public int Id { get; set; }
    public Models.Agency? Agency { get; set; }
    public int FeedId { get; set; }
    public int FeedScope { get; set; }
    public int? ScopeOverride { get; set; }
    public string? GtfsRouteId { get; set; }
    public string? ShortName { get; set; }
    public string? LongName { get; set; }
    public int RouteType { get; set; }
    public string? Color { get; set; }
    public string? TextColor { get; set; }
    public int? StopCount { get; set; }
    public double? LongestTripLengthMeters { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedDate { get; set; }
}
