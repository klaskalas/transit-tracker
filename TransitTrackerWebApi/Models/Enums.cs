namespace TransitTrackerWebApi.Models;

public enum GtfsRouteType
{
  // Railway Services (100-199)
  RailwayService = 100,
  HighSpeedRail = 101,
  LongDistanceTrains = 102,
  InterRegionalRail = 103,
  CarTransportRail = 104,
  SleeperRail = 105,
  RegionalRail = 106,
  TouristRailway = 107,
  RailShuttle = 108,
  SuburbanRailway = 109,
  ReplacementRail = 110,
  SpecialRail = 111,
  LorryTransportRail = 112,
  AllRailServices = 113,
  CrossCountryRail = 114,
  VehicleTransportRail = 115,
  RackAndPinionRailway = 116,
  AdditionalRail = 117,

  // Coach Services (200-299)
  CoachService = 200,
  InternationalCoach = 201,
  NationalCoach = 202,
  ShuttleCoach = 203,
  RegionalCoach = 204,
  SpecialCoach = 205,
  SightseeingCoach = 206,
  TouristCoach = 207,
  CommuterCoach = 208,
  AllCoachServices = 209,

  // Urban Railway Services (400-499)
  UrbanRailway = 400,
  Metro = 401,
  Underground = 402,
  UrbanRailwayService = 403,
  AllUrbanRailways = 404,
  Monorail = 405,

  // Bus Services (700-799)
  BusService = 700,
  RegionalBus = 701,
  ExpressBus = 702,
  StoppingBus = 703,
  LocalBus = 704,
  NightBus = 705,
  PostBus = 706,
  SpecialNeedsBus = 707,
  MobilityBus = 708,
  MobilityBusDisabled = 709,
  SightseeingBus = 710,
  ShuttleBus = 711,
  SchoolBus = 712,
  SchoolAndPublicBus = 713,
  RailReplacementBus = 714,
  DemandResponseBus = 715,
  AllBusServices = 716,

  // Trolleybus Service (800-899)
  Trolleybus = 800,

  // Tram Services (900-999)
  TramService = 900,
  CityTram = 901,
  LocalTram = 902,
  RegionalTram = 903,
  SightseeingTram = 904,
  ShuttleTram = 905,
  AllTramServices = 906,

  // Other Transport Services (1000+)
  WaterTransport = 1000,
  AirService = 1100,
  FerryService = 1200,
  AerialLift = 1300,
  Telecabin = 1301,
  CableCar = 1302,
  Elevator = 1303,
  ChairLift = 1304,
  DragLift = 1305,
  SmallTelecabin = 1306,
  AllTelecabinServices = 1307,
  Funicular = 1400,
  TaxiService = 1500,
  CommunalTaxi = 1501,
  WaterTaxi = 1502,
  RailTaxi = 1503,
  BikeTaxi = 1504,
  LicensedTaxi = 1505,
  PrivateHireVehicle = 1506,
  AllTaxiServices = 1507,
  MiscellaneousService = 1700,
  HorseDrawnCarriage = 1702

}
