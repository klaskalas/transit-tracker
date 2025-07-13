namespace TransitTrackerWebApi.Models;

public enum GtfsRouteType
{
    Tram = 0,
    Subway = 1,
    Rail = 2,
    Bus = 3,
    Ferry = 4,
    CableCar = 5,
    Gondola = 6,
    Funicular = 7,

    HighSpeedRail = 100,
    LongDistanceTrain = 101,
    RegionalRail = 102,
    LocalTrain = 103,
    BusRapidTransit = 200,
    ExpressBus = 201,
    LocalBus = 202,
    Trolleybus = 400,
    Monorail = 401,
    WaterTransport = 1000
}
