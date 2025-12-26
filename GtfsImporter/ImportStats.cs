namespace GtfsImporter;

internal sealed class ImportStats
{
    public int RegionsCreated { get; set; }
    public int FeedsCreated { get; set; }
    public int AgenciesInserted { get; set; }
    public int RoutesInserted { get; set; }
    public int RoutesUpdated { get; set; }
    public int RoutesArchived { get; set; }
    public int RoutesDeleted { get; set; }
    public int ShapesInserted { get; set; }
    public int TripsInserted { get; set; }
    public int ShapeLinesInserted { get; set; }
    public int RouteShapesInserted { get; set; }
}
