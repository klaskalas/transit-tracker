using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Npgsql;
using GtfsImporter;

const string defaultConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=admin;Database=transit-tracker";
const string defaultGtfsDir = @"C:\Users\User\Documents\gtfs";

string connectionString = GetArg(args, "connection") ??
                          Environment.GetEnvironmentVariable("GTFS_CONNECTION") ??
                          defaultConnectionString;
string gtfsDir = GetArg(args, "gtfs-dir") ?? defaultGtfsDir;
string? regionIdArg = GetArg(args, "region-id");
string regionName = GetArg(args, "region-name") ?? "Stockholm Region";
string countryCode = GetArg(args, "country-code") ?? "SE";
string? feedIdArg = GetArg(args, "feed-id");
string feedName = GetArg(args, "feed-name") ?? "Stockholm GTFS";
string feedSource = GetArg(args, "source-url") ?? "manual";
string scopeArg = GetArg(args, "scope") ?? "regional";
string replaceMode = (GetArg(args, "replace-mode") ?? "archive").ToLowerInvariant();
string updateMode = (GetArg(args, "update-mode") ?? "full").ToLowerInvariant();
bool dryRun = ParseBool(GetArg(args, "dry-run"));
int commandTimeoutSeconds = ParseInt(GetArg(args, "command-timeout"), 120);

var feedScope = ParseScope(scopeArg);

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();

await using var tx = await conn.BeginTransactionAsync();
await using (var timeoutCmd = new NpgsqlCommand($"SET statement_timeout = {commandTimeoutSeconds * 1000};", conn, tx))
{
    await timeoutCmd.ExecuteNonQueryAsync();
}
var stats = new ImportStats();

var regionId = await GetOrCreateRegion(conn, tx, regionIdArg, regionName, countryCode, feedScope, stats);
var feedId = await GetOrCreateFeed(conn, tx, feedIdArg, regionId, feedName, feedSource, feedScope, stats);

await UpdateFeedImportInfo(conn, tx, feedId, feedScope);

Console.WriteLine($"Importing GTFS from {gtfsDir} into feed {feedId}...");

await EnsureStagingTables(conn, tx);

var agenciesTotal = CountDataRows(Path.Combine(gtfsDir, "agency.txt"));
var routesTotal = CountDataRows(Path.Combine(gtfsDir, "routes.txt"));
var shapesTotal = CountDataRows(Path.Combine(gtfsDir, "shapes.txt"));
var tripsTotal = CountDataRows(Path.Combine(gtfsDir, "trips.txt"));

var agencyResult = await ImportAgencies(conn, tx, gtfsDir, countryCode, agenciesTotal);
stats.AgenciesInserted = agencyResult.Inserted;
var agencyMap = agencyResult.Map;
var defaultAgencyId = agencyMap.Values.FirstOrDefault();

if (defaultAgencyId == 0)
{
    throw new InvalidOperationException("No agencies found in agency.txt.");
}

var routeStats = await ImportRoutes(conn, tx, gtfsDir, feedId, agencyMap, defaultAgencyId, routesTotal);
stats.RoutesInserted = routeStats.Inserted;
stats.RoutesUpdated = routeStats.Updated;

var routesNew = await LoadRoutesNew(conn, tx, updateMode);
stats.TripsInserted = await ImportTrips(conn, tx, gtfsDir, tripsTotal, routesNew);
stats.StopTimesInserted = await ImportStopTimes(conn, tx, gtfsDir, routesNew);
await ImportStops(conn, tx, gtfsDir);
var shapesFilter = updateMode == "routes" ? await LoadShapesFilter(conn, tx) : null;
stats.ShapesInserted = await ImportShapes(conn, tx, gtfsDir, shapesTotal, updateMode, shapesFilter);

var shapeStats = await RefreshRouteShapes(conn, tx, feedId, updateMode);
stats.ShapeLinesInserted = shapeStats.ShapeLinesInserted;
stats.RouteShapesInserted = shapeStats.RouteShapesInserted;

stats.RoutesWithStopsUpdated = await UpdateStopCounts(conn, tx, feedId);
await UpdateRouteLongestTripLengths(conn, tx, feedId, updateMode);

var replaceStats = await ApplyReplaceMode(conn, tx, feedId, replaceMode);
stats.RoutesArchived = replaceStats.Archived;
stats.RoutesDeleted = replaceStats.Deleted;

if (dryRun)
{
    await tx.RollbackAsync();
    Console.WriteLine("Dry run complete. No changes were saved.");
}
else
{
    await tx.CommitAsync();
    Console.WriteLine("Import finished.");
}

PrintSummary(stats, replaceMode, dryRun);

static string? GetArg(string[] args, string name)
{
    var prefix = $"--{name}=";
    foreach (var arg in args)
    {
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return arg[prefix.Length..];
        }
    }
    return null;
}

static int ParseScope(string scope)
{
    return scope.Trim().ToLowerInvariant() switch
    {
        "regional" => 1,
        "national" => 2,
        "international" => 3,
        _ => 0
    };
}

static int ParseInt(string? value, int defaultValue)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    return int.TryParse(value, out var parsed) ? parsed : defaultValue;
}

static bool ParseBool(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    return value.Trim().ToLowerInvariant() switch
    {
        "1" => true,
        "true" => true,
        "yes" => true,
        "y" => true,
        _ => false
    };
}

static async Task<int> GetOrCreateRegion(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string? regionIdArg,
    string regionName,
    string countryCode,
    int scope,
    ImportStats stats)
{
    if (!string.IsNullOrWhiteSpace(regionIdArg) && int.TryParse(regionIdArg, out var regionId))
    {
        return regionId;
    }

    const string selectSql = @"
        SELECT id
        FROM regions
        WHERE name = @name AND country_code = @country;";

    await using (var selectCmd = new NpgsqlCommand(selectSql, conn, tx))
    {
        selectCmd.Parameters.AddWithValue("name", regionName);
        selectCmd.Parameters.AddWithValue("country", countryCode);
        var existing = await selectCmd.ExecuteScalarAsync();
        if (existing is int id)
        {
            return id;
        }
    }

    const string insertSql = @"
        INSERT INTO regions (name, country_code, scope, is_active)
        VALUES (@name, @country, @scope, TRUE)
        RETURNING id;";

    await using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
    insertCmd.Parameters.AddWithValue("name", regionName);
    insertCmd.Parameters.AddWithValue("country", countryCode);
    insertCmd.Parameters.AddWithValue("scope", scope);
    var newId = (int)(await insertCmd.ExecuteScalarAsync() ?? throw new InvalidOperationException("Failed to create region."));
    stats.RegionsCreated++;
    return newId;
}

static async Task<int> GetOrCreateFeed(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string? feedIdArg,
    int regionId,
    string feedName,
    string feedSource,
    int scope,
    ImportStats stats)
{
    if (!string.IsNullOrWhiteSpace(feedIdArg) && int.TryParse(feedIdArg, out var feedId))
    {
        return feedId;
    }

    const string selectSql = @"
        SELECT id
        FROM feeds
        WHERE region_id = @regionId AND name = @name;";

    await using (var selectCmd = new NpgsqlCommand(selectSql, conn, tx))
    {
        selectCmd.Parameters.AddWithValue("regionId", regionId);
        selectCmd.Parameters.AddWithValue("name", feedName);
        var existing = await selectCmd.ExecuteScalarAsync();
        if (existing is int id)
        {
            return id;
        }
    }

    const string insertSql = @"
        INSERT INTO feeds (region_id, name, source_url, scope, is_active)
        VALUES (@regionId, @name, @sourceUrl, @scope, TRUE)
        RETURNING id;";

    await using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
    insertCmd.Parameters.AddWithValue("regionId", regionId);
    insertCmd.Parameters.AddWithValue("name", feedName);
    insertCmd.Parameters.AddWithValue("sourceUrl", feedSource);
    insertCmd.Parameters.AddWithValue("scope", scope);
    var newId = (int)(await insertCmd.ExecuteScalarAsync() ?? throw new InvalidOperationException("Failed to create feed."));
    stats.FeedsCreated++;
    return newId;
}

static async Task UpdateFeedImportInfo(NpgsqlConnection conn, NpgsqlTransaction tx, int feedId, int scope)
{
    const string sql = @"
        UPDATE feeds
        SET imported_at = NOW(),
            scope = @scope,
            is_active = TRUE
        WHERE id = @feedId;";

    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    cmd.Parameters.AddWithValue("scope", scope);
    cmd.Parameters.AddWithValue("feedId", feedId);
    await cmd.ExecuteNonQueryAsync();
}

static CsvConfiguration CreateCsvConfig() => new(CultureInfo.InvariantCulture)
{
    MissingFieldFound = null,
    HeaderValidated = null,
    BadDataFound = null
};

static string? GetField(IDictionary<string, object> row, string key)
{
    return row.TryGetValue(key, out var value) ? value?.ToString() : null;
}

static async Task EnsureStagingTables(NpgsqlConnection conn, NpgsqlTransaction tx)
{
    const string sql = @"
        CREATE TEMP TABLE IF NOT EXISTS routes_staging (
            gtfs_route_id TEXT PRIMARY KEY
        );
        CREATE TEMP TABLE IF NOT EXISTS routes_new (
            gtfs_route_id TEXT PRIMARY KEY
        );
        CREATE TEMP TABLE IF NOT EXISTS shapes_staging (
            gtfs_shape_id TEXT PRIMARY KEY
        );
        CREATE TEMP TABLE IF NOT EXISTS shapes_new (
            gtfs_shape_id TEXT PRIMARY KEY
        );
        CREATE TEMP TABLE IF NOT EXISTS shapes_import (
            gtfs_shape_id TEXT,
            sequence INTEGER,
            lat DOUBLE PRECISION,
            lon DOUBLE PRECISION
        );
        CREATE TEMP TABLE IF NOT EXISTS trips_staging (
            trip_id TEXT,
            route_id TEXT,
            shape_id TEXT
        );
        CREATE TEMP TABLE IF NOT EXISTS stop_times_staging (
            trip_id TEXT,
            stop_id TEXT
        );
        CREATE TEMP TABLE IF NOT EXISTS stops_staging (
            stop_id TEXT PRIMARY KEY,
            parent_station TEXT
        );";

    await using (var cmd = new NpgsqlCommand(sql, conn, tx))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    await using (var cmd = new NpgsqlCommand("TRUNCATE routes_staging, routes_new, shapes_staging, shapes_new, shapes_import, trips_staging, stop_times_staging, stops_staging;", conn, tx))
    {
        await cmd.ExecuteNonQueryAsync();
    }
}

static async Task<(Dictionary<string, int> Map, int Inserted)> ImportAgencies(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string gtfsDir,
    string countryCode,
    int totalRecords)
{
    var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var inserted = 0;
    var processed = 0;
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var lastLog = TimeSpan.Zero;
    UpdateProgress("Importing agencies", processed, totalRecords, timer, ref lastLog);

    using var reader = new StreamReader(Path.Combine(gtfsDir, "agency.txt"));
    using var csv = new CsvReader(reader, CreateCsvConfig());

    foreach (var record in csv.GetRecords<dynamic>())
    {
        processed++;
        var row = (IDictionary<string, object>)record;
        var agencyName = GetField(row, "agency_name") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(agencyName))
        {
            UpdateProgress("Importing agencies", processed, totalRecords, timer, ref lastLog);
            continue;
        }

        var agencyUrl = GetField(row, "agency_url");
        var timezone = GetField(row, "agency_timezone");
        var agencyId = GetField(row, "agency_id") ?? agencyName;

        const string selectSql = @"
            SELECT id
            FROM agencies
            WHERE name = @name AND country_code = @country;";
        await using var selectCmd = new NpgsqlCommand(selectSql, conn, tx);
        selectCmd.Parameters.AddWithValue("name", agencyName);
        selectCmd.Parameters.AddWithValue("country", countryCode);
        var existing = await selectCmd.ExecuteScalarAsync();

        int dbId;
        if (existing is int id)
        {
            dbId = id;
        }
        else
        {
            const string insertSql = @"
                INSERT INTO agencies (name, country_code, agency_url, timezone)
                VALUES (@name, @country, @url, @tz)
                RETURNING id;";
            await using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
            insertCmd.Parameters.AddWithValue("name", agencyName);
            insertCmd.Parameters.AddWithValue("country", countryCode);
            insertCmd.Parameters.AddWithValue("url", (object?)agencyUrl ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("tz", (object?)timezone ?? DBNull.Value);
            dbId = (int)(await insertCmd.ExecuteScalarAsync() ?? throw new InvalidOperationException("Failed to insert agency."));
            inserted++;
        }

        map[agencyId] = dbId;
        UpdateProgress("Importing agencies", processed, totalRecords, timer, ref lastLog);
    }

    UpdateProgress("Importing agencies", totalRecords, totalRecords, timer, ref lastLog, true);
    return (map, inserted);
}

static async Task<(int Inserted, int Updated)> ImportRoutes(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string gtfsDir,
    int feedId,
    Dictionary<string, int> agencyMap,
    int defaultAgencyId,
    int totalRecords)
{
    var inserted = 0;
    var updated = 0;
    var processed = 0;
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var lastLog = TimeSpan.Zero;
    UpdateProgress("Importing routes", processed, totalRecords, timer, ref lastLog);

    using var reader = new StreamReader(Path.Combine(gtfsDir, "routes.txt"));
    using var csv = new CsvReader(reader, CreateCsvConfig());

    foreach (var record in csv.GetRecords<dynamic>())
    {
        processed++;
        UpdateProgress("Importing routes", processed, totalRecords, timer, ref lastLog);

        var row = (IDictionary<string, object>)record;
        var routeId = GetField(row, "route_id");
        if (string.IsNullOrWhiteSpace(routeId))
        {
            continue;
        }

        var shortName = GetField(row, "route_short_name") ?? string.Empty;
        var longName = GetField(row, "route_long_name") ?? string.Empty;
        var routeTypeRaw = GetField(row, "route_type") ?? "3";
        var agencyIdRaw = GetField(row, "agency_id");

        _ = int.TryParse(routeTypeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var routeType);

        var agencyId = defaultAgencyId;
        if (!string.IsNullOrWhiteSpace(agencyIdRaw) && agencyMap.TryGetValue(agencyIdRaw, out var mappedId))
        {
            agencyId = mappedId;
        }

        const string stagingSql = @"INSERT INTO routes_staging (gtfs_route_id) VALUES (@id) ON CONFLICT DO NOTHING;";
        await using (var stagingCmd = new NpgsqlCommand(stagingSql, conn, tx))
        {
            stagingCmd.Parameters.AddWithValue("id", routeId);
            await stagingCmd.ExecuteNonQueryAsync();
        }

        const string selectSql = @"
            SELECT id
            FROM routes
            WHERE feed_id = @feedId AND gtfs_route_id = @routeId;";
        await using var selectCmd = new NpgsqlCommand(selectSql, conn, tx);
        selectCmd.Parameters.AddWithValue("feedId", feedId);
        selectCmd.Parameters.AddWithValue("routeId", routeId);
        var existing = await selectCmd.ExecuteScalarAsync();

        if (existing is int routeDbId)
        {
            const string updateSql = @"
                UPDATE routes
                SET agency_id = @agencyId,
                    short_name = @shortName,
                    long_name = @longName,
                    route_type = @routeType,
                    is_active = TRUE
                WHERE id = @id;";
            await using var updateCmd = new NpgsqlCommand(updateSql, conn, tx);
            updateCmd.Parameters.AddWithValue("agencyId", agencyId);
            updateCmd.Parameters.AddWithValue("shortName", shortName);
            updateCmd.Parameters.AddWithValue("longName", longName);
            updateCmd.Parameters.AddWithValue("routeType", routeType);
            updateCmd.Parameters.AddWithValue("id", routeDbId);
            await updateCmd.ExecuteNonQueryAsync();
            updated++;
        }
        else
        {
            const string insertSql = @"
                INSERT INTO routes (feed_id, agency_id, gtfs_route_id, short_name, long_name, route_type, is_active)
                VALUES (@feedId, @agencyId, @routeId, @shortName, @longName, @routeType, TRUE);";
            await using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
            insertCmd.Parameters.AddWithValue("feedId", feedId);
            insertCmd.Parameters.AddWithValue("agencyId", agencyId);
            insertCmd.Parameters.AddWithValue("routeId", routeId);
            insertCmd.Parameters.AddWithValue("shortName", shortName);
            insertCmd.Parameters.AddWithValue("longName", longName);
            insertCmd.Parameters.AddWithValue("routeType", routeType);
            await insertCmd.ExecuteNonQueryAsync();
            await using (var newCmd = new NpgsqlCommand("INSERT INTO routes_new (gtfs_route_id) VALUES (@id) ON CONFLICT DO NOTHING;", conn, tx))
            {
                newCmd.Parameters.AddWithValue("id", routeId);
                await newCmd.ExecuteNonQueryAsync();
            }
            inserted++;
        }
    }

    UpdateProgress("Importing routes", totalRecords, totalRecords, timer, ref lastLog, true);
    return (inserted, updated);
}

static async Task<int> ImportShapes(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string gtfsDir,
    int totalRecords,
    string updateMode,
    HashSet<string>? shapesFilter)
{
    var inserted = 0;
    var processed = 0;
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var lastLog = TimeSpan.Zero;
    var shapeIds = new HashSet<string>(StringComparer.Ordinal);
    const int batchSize = 1000;
    var shapeIdBatch = new List<string>(batchSize);
    var sequenceBatch = new List<int>(batchSize);
    var latBatch = new List<double>(batchSize);
    var lonBatch = new List<double>(batchSize);
    UpdateProgress("Importing shapes", processed, totalRecords, timer, ref lastLog);

    using var reader = new StreamReader(Path.Combine(gtfsDir, "shapes.txt"));
    using var csv = new CsvReader(reader, CreateCsvConfig());

    foreach (var record in csv.GetRecords<dynamic>())
    {
        processed++;
        UpdateProgress("Importing shapes", processed, totalRecords, timer, ref lastLog);

        var row = (IDictionary<string, object>)record;
        var shapeId = GetField(row, "shape_id");
        var latRaw = GetField(row, "shape_pt_lat");
        var lonRaw = GetField(row, "shape_pt_lon");
        var seqRaw = GetField(row, "shape_pt_sequence");

        if (string.IsNullOrWhiteSpace(shapeId) ||
            string.IsNullOrWhiteSpace(latRaw) ||
            string.IsNullOrWhiteSpace(lonRaw) ||
            string.IsNullOrWhiteSpace(seqRaw))
        {
            continue;
        }

        if (updateMode == "routes" && (shapesFilter == null || !shapesFilter.Contains(shapeId)))
        {
            continue;
        }

        var lat = double.Parse(latRaw, CultureInfo.InvariantCulture);
        var lon = double.Parse(lonRaw, CultureInfo.InvariantCulture);
        var sequence = int.Parse(seqRaw, CultureInfo.InvariantCulture);

        shapeIdBatch.Add(shapeId);
        sequenceBatch.Add(sequence);
        latBatch.Add(lat);
        lonBatch.Add(lon);
        if (shapeIdBatch.Count >= batchSize)
        {
            await InsertShapesImportBatch(conn, tx, shapeIdBatch, sequenceBatch, latBatch, lonBatch);
            shapeIdBatch.Clear();
            sequenceBatch.Clear();
            latBatch.Clear();
            lonBatch.Clear();
        }

        shapeIds.Add(shapeId);
        inserted++;
    }

    if (shapeIdBatch.Count > 0)
    {
        await InsertShapesImportBatch(conn, tx, shapeIdBatch, sequenceBatch, latBatch, lonBatch);
    }
    await BulkInsertShapeIds(conn, tx, "shapes_staging", shapeIds);
    await BulkInsertShapeIds(conn, tx, "shapes_new", shapeIds);

    if (updateMode != "routes")
    {
        const string cleanupSql = @"
            DELETE FROM route_shapes WHERE gtfs_shape_id IN (SELECT gtfs_shape_id FROM shapes_staging);
            DELETE FROM shape_lines WHERE gtfs_shape_id IN (SELECT gtfs_shape_id FROM shapes_staging);
            DELETE FROM shapes WHERE gtfs_shape_id IN (SELECT gtfs_shape_id FROM shapes_staging);";
        await using (var cleanupCmd = new NpgsqlCommand(cleanupSql, conn, tx))
        {
            cleanupCmd.CommandTimeout = 0;
            await cleanupCmd.ExecuteNonQueryAsync();
        }
    }

    const string removeExistingNewSql = @"
        DELETE FROM shapes_new
        WHERE gtfs_shape_id IN (SELECT gtfs_shape_id FROM shapes);";
    await using (var removeCmd = new NpgsqlCommand(removeExistingNewSql, conn, tx))
    {
        removeCmd.CommandTimeout = 0;
        await removeCmd.ExecuteNonQueryAsync();
    }

    const string insertSql = @"
        INSERT INTO shapes (gtfs_shape_id, sequence, lat, lon)
        SELECT gtfs_shape_id,
               sequence,
               lat,
               lon
        FROM shapes_import
        WHERE gtfs_shape_id IN (SELECT gtfs_shape_id FROM shapes_new);";
    await using (var insertCmd = new NpgsqlCommand(insertSql, conn, tx))
    {
        insertCmd.CommandTimeout = 0;
        await insertCmd.ExecuteNonQueryAsync();
    }

    UpdateProgress("Importing shapes", totalRecords, totalRecords, timer, ref lastLog, true);
    return inserted;
}

static async Task BulkInsertShapeIds(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string tableName,
    IReadOnlyCollection<string> shapeIds)
{
    if (shapeIds.Count == 0)
    {
        return;
    }

    Console.WriteLine($"Writing {shapeIds.Count} shape ids to {tableName}...");

    const int batchSize = 1000;
    var batch = new List<string>(batchSize);
    foreach (var shapeId in shapeIds)
    {
        batch.Add(shapeId);
        if (batch.Count < batchSize)
        {
            continue;
        }

        await InsertShapeIdBatch(conn, tx, tableName, batch);
        batch.Clear();
    }

    if (batch.Count > 0)
    {
        await InsertShapeIdBatch(conn, tx, tableName, batch);
    }

    Console.WriteLine($"Finished writing shape ids to {tableName}.");
}

static async Task InsertShapeIdBatch(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string tableName,
    List<string> batch)
{
    var sql = $"INSERT INTO {tableName} (gtfs_shape_id) SELECT unnest(@ids) ON CONFLICT DO NOTHING;";
    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    cmd.Parameters.AddWithValue("ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, batch.ToArray());
    await cmd.ExecuteNonQueryAsync();
}

static async Task InsertShapesImportBatch(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    List<string> shapeIds,
    List<int> sequences,
    List<double> lats,
    List<double> lons)
{
    const string sql = @"
        INSERT INTO shapes_import (gtfs_shape_id, sequence, lat, lon)
        SELECT *
        FROM unnest(@shapeIds, @sequences, @lats, @lons);";
    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    cmd.Parameters.AddWithValue("shapeIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, shapeIds.ToArray());
    cmd.Parameters.AddWithValue("sequences", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer, sequences.ToArray());
    cmd.Parameters.AddWithValue("lats", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Double, lats.ToArray());
    cmd.Parameters.AddWithValue("lons", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Double, lons.ToArray());
    await cmd.ExecuteNonQueryAsync();
}

static async Task<int> ImportTrips(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string gtfsDir,
    int totalRecords,
    HashSet<string>? routesFilter)
{
    var inserted = 0;
    var processed = 0;
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var lastLog = TimeSpan.Zero;
    UpdateProgress("Importing trips", processed, totalRecords, timer, ref lastLog);

    using var reader = new StreamReader(Path.Combine(gtfsDir, "trips.txt"));
    using var csv = new CsvReader(reader, CreateCsvConfig());

    foreach (var record in csv.GetRecords<dynamic>())
    {
        processed++;
        UpdateProgress("Importing trips", processed, totalRecords, timer, ref lastLog);

        var row = (IDictionary<string, object>)record;
        var tripId = GetField(row, "trip_id");
        var routeId = GetField(row, "route_id");
        var shapeId = GetField(row, "shape_id");

        if (string.IsNullOrWhiteSpace(tripId) || string.IsNullOrWhiteSpace(routeId) || string.IsNullOrWhiteSpace(shapeId))
        {
            continue;
        }

        if (routesFilter is not null && !routesFilter.Contains(routeId))
        {
            continue;
        }

        const string insertSql = @"
            INSERT INTO trips_staging (trip_id, route_id, shape_id)
            VALUES (@tripId, @routeId, @shapeId);";
        await using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
        insertCmd.Parameters.AddWithValue("tripId", tripId);
        insertCmd.Parameters.AddWithValue("routeId", routeId);
        insertCmd.Parameters.AddWithValue("shapeId", shapeId);
        await insertCmd.ExecuteNonQueryAsync();
        inserted++;
    }

    UpdateProgress("Importing trips", totalRecords, totalRecords, timer, ref lastLog, true);
    return inserted;
}

static async Task<int> ImportStopTimes(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string gtfsDir,
    HashSet<string>? routesFilter)
{
    var inserted = 0;
    using var reader = new StreamReader(Path.Combine(gtfsDir, "stop_times.txt"));
    using var csv = new CsvReader(reader, CreateCsvConfig());
    const int batchSize = 1000;
    var tripBatch = new List<string>(batchSize);
    var stopBatch = new List<string>(batchSize);

    foreach (var record in csv.GetRecords<dynamic>())
    {
        var row = (IDictionary<string, object>)record;
        var tripId = GetField(row, "trip_id");
        var stopId = GetField(row, "stop_id");

        if (string.IsNullOrWhiteSpace(tripId) || string.IsNullOrWhiteSpace(stopId))
        {
            continue;
        }

        if (routesFilter is not null)
        {
            const string existsSql = @"SELECT 1 FROM trips_staging WHERE trip_id = @tripId LIMIT 1;";
            await using var existsCmd = new NpgsqlCommand(existsSql, conn, tx);
            existsCmd.Parameters.AddWithValue("tripId", tripId);
            var exists = await existsCmd.ExecuteScalarAsync();
            if (exists is null)
            {
                continue;
            }
        }

        tripBatch.Add(tripId);
        stopBatch.Add(stopId);
        if (tripBatch.Count >= batchSize)
        {
            await InsertStopTimesBatch(conn, tx, tripBatch, stopBatch);
            tripBatch.Clear();
            stopBatch.Clear();
        }
        inserted++;
    }

    if (tripBatch.Count > 0)
    {
        await InsertStopTimesBatch(conn, tx, tripBatch, stopBatch);
    }
    return inserted;
}

static async Task InsertStopTimesBatch(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    List<string> tripIds,
    List<string> stopIds)
{
    const string sql = @"
        INSERT INTO stop_times_staging (trip_id, stop_id)
        SELECT *
        FROM unnest(@tripIds, @stopIds);";
    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    cmd.Parameters.AddWithValue("tripIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, tripIds.ToArray());
    cmd.Parameters.AddWithValue("stopIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, stopIds.ToArray());
    await cmd.ExecuteNonQueryAsync();
}

static async Task<int> ImportStops(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    string gtfsDir)
{
    var stopsPath = Path.Combine(gtfsDir, "stops.txt");
    if (!File.Exists(stopsPath))
    {
        return 0;
    }

    var inserted = 0;
    const int batchSize = 1000;
    var stopIdBatch = new List<string>(batchSize);
    var parentBatch = new List<string?>(batchSize);

    using var reader = new StreamReader(stopsPath);
    using var csv = new CsvReader(reader, CreateCsvConfig());

    foreach (var record in csv.GetRecords<dynamic>())
    {
        var row = (IDictionary<string, object>)record;
        var stopId = GetField(row, "stop_id");
        if (string.IsNullOrWhiteSpace(stopId))
        {
            continue;
        }

        var parentStation = GetField(row, "parent_station");
        if (string.IsNullOrWhiteSpace(parentStation))
        {
            parentStation = null;
        }

        stopIdBatch.Add(stopId);
        parentBatch.Add(parentStation);
        inserted++;

        if (stopIdBatch.Count >= batchSize)
        {
            await InsertStopsBatch(conn, tx, stopIdBatch, parentBatch);
            stopIdBatch.Clear();
            parentBatch.Clear();
        }
    }

    if (stopIdBatch.Count > 0)
    {
        await InsertStopsBatch(conn, tx, stopIdBatch, parentBatch);
    }

    return inserted;
}

static async Task InsertStopsBatch(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    List<string> stopIds,
    List<string?> parentStations)
{
    const string sql = @"
        INSERT INTO stops_staging (stop_id, parent_station)
        SELECT *
        FROM unnest(@stopIds, @parentStations)
        ON CONFLICT (stop_id) DO NOTHING;";
    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    cmd.Parameters.AddWithValue("stopIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, stopIds.ToArray());
    cmd.Parameters.AddWithValue("parentStations", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, parentStations.ToArray());
    await cmd.ExecuteNonQueryAsync();
}

static async Task<(int ShapeLinesInserted, int RouteShapesInserted)> RefreshRouteShapes(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    int feedId,
    string updateMode)
{
    Console.WriteLine("Refreshing route shapes...");

    if (updateMode != "routes")
    {
        const string clearSql = @"
            DELETE FROM route_shapes
            WHERE route_id IN (SELECT id FROM routes WHERE feed_id = @feedId);";
        await using (var clearCmd = new NpgsqlCommand(clearSql, conn, tx))
        {
            clearCmd.Parameters.AddWithValue("feedId", feedId);
            await clearCmd.ExecuteNonQueryAsync();
        }
    }

    var shapeLineSql = updateMode == "routes"
        ? @"
            INSERT INTO shape_lines (gtfs_shape_id, geom)
            SELECT gtfs_shape_id,
                   ST_MakeLine(ST_SetSRID(ST_MakePoint(lon, lat), 4326) ORDER BY sequence)
            FROM shapes
            WHERE gtfs_shape_id IN (SELECT gtfs_shape_id FROM shapes_new)
            GROUP BY gtfs_shape_id
            ON CONFLICT (gtfs_shape_id) DO NOTHING;"
        : @"
            INSERT INTO shape_lines (gtfs_shape_id, geom)
            SELECT gtfs_shape_id,
                   ST_MakeLine(ST_SetSRID(ST_MakePoint(lon, lat), 4326) ORDER BY sequence)
            FROM shapes
            WHERE gtfs_shape_id IN (SELECT gtfs_shape_id FROM shapes_staging)
            GROUP BY gtfs_shape_id;";
    int shapeLinesInserted;
    await using (var shapeLineCmd = new NpgsqlCommand(shapeLineSql, conn, tx))
    {
        shapeLineCmd.CommandTimeout = 0;
        shapeLinesInserted = await shapeLineCmd.ExecuteNonQueryAsync();
    }

    var routeShapeSql = updateMode == "routes"
        ? @"
            INSERT INTO route_shapes (route_id, gtfs_shape_id)
            SELECT r.id, t.shape_id
            FROM routes r
            JOIN (
                SELECT DISTINCT route_id, shape_id FROM trips_staging
            ) t ON r.gtfs_route_id = t.route_id
            WHERE r.feed_id = @feedId
              AND r.is_active = TRUE
              AND r.gtfs_route_id IN (SELECT gtfs_route_id FROM routes_new);"
        : @"
            INSERT INTO route_shapes (route_id, gtfs_shape_id)
            SELECT r.id, t.shape_id
            FROM routes r
            JOIN (
                SELECT DISTINCT route_id, shape_id FROM trips_staging
            ) t ON r.gtfs_route_id = t.route_id
            WHERE r.feed_id = @feedId AND r.is_active = TRUE;";
    int routeShapesInserted;
    await using (var cmd = new NpgsqlCommand(routeShapeSql, conn, tx))
    {
        cmd.Parameters.AddWithValue("feedId", feedId);
        cmd.CommandTimeout = 0;
        routeShapesInserted = await cmd.ExecuteNonQueryAsync();
    }

    return (shapeLinesInserted, routeShapesInserted);
}

static async Task<(int Archived, int Deleted)> ApplyReplaceMode(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    int feedId,
    string replaceMode)
{
    if (replaceMode == "keep")
    {
        return (0, 0);
    }

    if (replaceMode == "archive")
    {
        const string countSql = @"
            SELECT COUNT(*)
            FROM routes
            WHERE feed_id = @feedId
              AND gtfs_route_id NOT IN (SELECT gtfs_route_id FROM routes_staging);";
        await using var countCmd = new NpgsqlCommand(countSql, conn, tx);
        countCmd.Parameters.AddWithValue("feedId", feedId);
        var archivedCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);

        const string archiveSql = @"
            UPDATE routes
            SET is_active = FALSE
            WHERE feed_id = @feedId
              AND gtfs_route_id NOT IN (SELECT gtfs_route_id FROM routes_staging);

            UPDATE routes
            SET is_active = TRUE
            WHERE feed_id = @feedId
              AND gtfs_route_id IN (SELECT gtfs_route_id FROM routes_staging);";
        await using var cmd = new NpgsqlCommand(archiveSql, conn, tx);
        cmd.Parameters.AddWithValue("feedId", feedId);
        await cmd.ExecuteNonQueryAsync();
        return (archivedCount, 0);
    }

    if (replaceMode == "delete")
    {
        const string deleteShapesSql = @"
            DELETE FROM route_shapes
            WHERE route_id IN (
                SELECT id
                FROM routes
                WHERE feed_id = @feedId
                  AND gtfs_route_id NOT IN (SELECT gtfs_route_id FROM routes_staging)
            );";
        await using (var cmd = new NpgsqlCommand(deleteShapesSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("feedId", feedId);
            await cmd.ExecuteNonQueryAsync();
        }

        const string deleteRoutesSql = @"
            DELETE FROM routes
            WHERE feed_id = @feedId
              AND gtfs_route_id NOT IN (SELECT gtfs_route_id FROM routes_staging);";
        await using (var cmd = new NpgsqlCommand(deleteRoutesSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("feedId", feedId);
            var deleted = await cmd.ExecuteNonQueryAsync();
            return (0, deleted);
        }
    }

    return (0, 0);
}

static void PrintSummary(ImportStats stats, string replaceMode, bool dryRun)
{
    Console.WriteLine("---- Import summary ----");
    Console.WriteLine($"Dry run: {dryRun}");
    Console.WriteLine($"Regions created: {stats.RegionsCreated}");
    Console.WriteLine($"Feeds created: {stats.FeedsCreated}");
    Console.WriteLine($"Agencies inserted: {stats.AgenciesInserted}");
    Console.WriteLine($"Routes inserted: {stats.RoutesInserted}");
    Console.WriteLine($"Routes updated: {stats.RoutesUpdated}");
    Console.WriteLine($"Shapes inserted: {stats.ShapesInserted}");
    Console.WriteLine($"Trips inserted: {stats.TripsInserted}");
    Console.WriteLine($"Stop times inserted: {stats.StopTimesInserted}");
    Console.WriteLine($"Routes updated with stops: {stats.RoutesWithStopsUpdated}");
    Console.WriteLine($"Shape lines inserted: {stats.ShapeLinesInserted}");
    Console.WriteLine($"Route shapes inserted: {stats.RouteShapesInserted}");
    if (replaceMode == "archive")
    {
        Console.WriteLine($"Routes archived: {stats.RoutesArchived}");
    }
    else if (replaceMode == "delete")
    {
        Console.WriteLine($"Routes deleted: {stats.RoutesDeleted}");
    }
    else
    {
        Console.WriteLine("Routes archived/deleted: 0 (replace-mode=keep)");
    }
}

static async Task<int> UpdateStopCounts(NpgsqlConnection conn, NpgsqlTransaction tx, int feedId)
{
    const string sql = @"
        WITH route_counts AS (
            SELECT t.route_id,
                   COUNT(DISTINCT COALESCE(s.parent_station, st.stop_id)) AS stop_count
            FROM trips_staging t
            JOIN stop_times_staging st ON st.trip_id = t.trip_id
            LEFT JOIN stops_staging s ON s.stop_id = st.stop_id
            GROUP BY t.route_id
        )
        UPDATE routes r
        SET stop_count = rc.stop_count
        FROM route_counts rc
        WHERE r.feed_id = @feedId
          AND r.gtfs_route_id = rc.route_id;";

    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    cmd.Parameters.AddWithValue("feedId", feedId);
    return await cmd.ExecuteNonQueryAsync();
}

static async Task<int> UpdateRouteLongestTripLengths(
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    int feedId,
    string updateMode)
{
    var scopeFilter = updateMode == "routes"
        ? " AND r.gtfs_route_id IN (SELECT gtfs_route_id FROM routes_new)"
        : string.Empty;

    var updateSql = $@"
        WITH shape_lengths AS (
            SELECT rs.route_id,
                   MAX(ST_Length(sl.geom::geography)) AS max_length_m
            FROM route_shapes rs
            JOIN shape_lines sl ON sl.gtfs_shape_id = rs.gtfs_shape_id
            JOIN routes r ON r.id = rs.route_id
            WHERE r.feed_id = @feedId{scopeFilter}
            GROUP BY rs.route_id
        )
        UPDATE routes r
        SET longest_trip_length_m = sl.max_length_m
        FROM shape_lengths sl
        WHERE r.id = sl.route_id;";

    int updated;
    await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
    {
        cmd.Parameters.AddWithValue("feedId", feedId);
        cmd.CommandTimeout = 0;
        updated = await cmd.ExecuteNonQueryAsync();
    }

    var clearSql = $@"
        UPDATE routes r
        SET longest_trip_length_m = NULL
        WHERE r.feed_id = @feedId{scopeFilter}
          AND NOT EXISTS (
              SELECT 1 FROM route_shapes rs WHERE rs.route_id = r.id
          );";
    await using (var cmd = new NpgsqlCommand(clearSql, conn, tx))
    {
        cmd.Parameters.AddWithValue("feedId", feedId);
        cmd.CommandTimeout = 0;
        await cmd.ExecuteNonQueryAsync();
    }

    return updated;
}

static int CountDataRows(string filePath)
{
    if (!File.Exists(filePath))
    {
        return 0;
    }

    var count = -1;
    foreach (var _ in File.ReadLines(filePath))
    {
        count++;
    }

    return Math.Max(0, count);
}

static void UpdateProgress(
    string label,
    int processed,
    int total,
    System.Diagnostics.Stopwatch timer,
    ref TimeSpan lastLog,
    bool force = false)
{
    if (!force && timer.Elapsed - lastLog < TimeSpan.FromSeconds(10))
    {
        return;
    }

    lastLog = timer.Elapsed;
    var percent = total == 0 ? 100 : (int)Math.Floor(processed * 100.0 / total);
    var safeTotal = total == 0 ? processed : total;
    Console.Write($"\r{label}... {percent}% ({processed:N0}/{safeTotal:N0})");
    if (force)
    {
        Console.WriteLine();
    }
}

static async Task<HashSet<string>?> LoadRoutesNew(NpgsqlConnection conn, NpgsqlTransaction tx, string updateMode)
{
    if (updateMode != "routes")
    {
        return null;
    }

    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    const string sql = @"SELECT gtfs_route_id FROM routes_new;";
    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        set.Add(reader.GetString(0));
    }

    return set;
}

static async Task<HashSet<string>?> LoadShapesFilter(NpgsqlConnection conn, NpgsqlTransaction tx)
{
    const string sql = @"
        INSERT INTO shapes_staging (gtfs_shape_id)
        SELECT DISTINCT shape_id
        FROM trips_staging
        WHERE shape_id IS NOT NULL
        ON CONFLICT DO NOTHING;";
    await using (var cmd = new NpgsqlCommand(sql, conn, tx))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    const string selectSql = @"SELECT gtfs_shape_id FROM shapes_staging;";
    await using var selectCmd = new NpgsqlCommand(selectSql, conn, tx);
    await using var reader = await selectCmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        set.Add(reader.GetString(0));
    }

    return set;
}
