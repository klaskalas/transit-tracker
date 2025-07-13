using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Npgsql;

string connectionString = "Host=localhost;Port=5432;Username=postgres;Password=admin;Database=transit-tracker";

string gtfsDir = @"C:\Users\User\Documents\gtfs";

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();

Console.WriteLine("Rensar gamla data...");
await using (var cmd = new NpgsqlCommand("TRUNCATE route_shapes, shape_lines, shapes, routes, agencies RESTART IDENTITY CASCADE", conn))
    await cmd.ExecuteNonQueryAsync();

// === Importera agency.txt ===
Console.WriteLine("Importerar agencies...");
using (var reader = new StreamReader(Path.Combine(gtfsDir, "agency.txt")))
using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
{
    var records = csv.GetRecords<dynamic>();
    foreach (var row in records)
    {
        var agencyName = row.agency_name;
        var agencyUrl = row.agency_url;
        var timezone = row.agency_timezone;
        var country = "SE"; // Anpassa efter behov

        var sql = @"INSERT INTO agencies (name, country_code, agency_url, timezone) VALUES (@name, @country, @url, @tz)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", agencyName);
        cmd.Parameters.AddWithValue("country", country);
        cmd.Parameters.AddWithValue("url", agencyUrl);
        cmd.Parameters.AddWithValue("tz", timezone);
        await cmd.ExecuteNonQueryAsync();
    }
}

// === Importera routes.txt ===
Console.WriteLine("Importerar routes...");
var agencyId = 1; // Just nu hårdkodat, stöd för flera kan läggas till
using (var reader = new StreamReader(Path.Combine(gtfsDir, "routes.txt")))
using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
{
    var records = csv.GetRecords<dynamic>();
    foreach (var row in records)
    {
        string routeId = row.route_id;
        string shortName = row.route_short_name ?? "";
        string longName = row.route_long_name ?? "";
        int routeType = int.Parse(row.route_type ?? "3");

        var sql = @"INSERT INTO routes (agency_id, gtfs_route_id, short_name, long_name, route_type)
                    VALUES (@agency_id, @gtfs_id, @short, @long, @type)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("agency_id", agencyId);
        cmd.Parameters.AddWithValue("gtfs_id", routeId);
        cmd.Parameters.AddWithValue("short", shortName);
        cmd.Parameters.AddWithValue("long", longName);
        cmd.Parameters.AddWithValue("type", routeType);
        await cmd.ExecuteNonQueryAsync();
    }
}

// === Importera shapes.txt ===
Console.WriteLine("Importerar shapes...");
using (var reader = new StreamReader(Path.Combine(gtfsDir, "shapes.txt")))
using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
{
    var records = csv.GetRecords<dynamic>();
    foreach (var row in records)
    {
        string shapeId = row.shape_id;
        double lat = double.Parse(row.shape_pt_lat, CultureInfo.InvariantCulture);
        double lon = double.Parse(row.shape_pt_lon, CultureInfo.InvariantCulture);
        int sequence = int.Parse(row.shape_pt_sequence);

        var sql = @"INSERT INTO shapes (gtfs_shape_id, sequence, lat, lon) VALUES (@id, @seq, @lat, @lon)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", shapeId);
        cmd.Parameters.AddWithValue("seq", sequence);
        cmd.Parameters.AddWithValue("lat", lat);
        cmd.Parameters.AddWithValue("lon", lon);
        await cmd.ExecuteNonQueryAsync();
    }
}

// === Skapa shape_lines ===
Console.WriteLine("Skapar shape_lines...");
string shapeLineSql = @"
INSERT INTO shape_lines (gtfs_shape_id, geom)
SELECT gtfs_shape_id,
       ST_MakeLine(geom ORDER BY sequence)
FROM shapes
GROUP BY gtfs_shape_id;";
await using (var cmd = new NpgsqlCommand(shapeLineSql, conn))
    await cmd.ExecuteNonQueryAsync();

// === Importera trips.txt till en temporär tabell ===
await using (var createCmd = new NpgsqlCommand(@"
    CREATE TABLE IF NOT EXISTS trips_staging (
        route_id TEXT,
        shape_id TEXT
    );", conn))
{
    await createCmd.ExecuteNonQueryAsync();
}

await using (var truncateCmd = new NpgsqlCommand("TRUNCATE trips_staging;", conn))
{
    await truncateCmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Importerar trips.txt...");
using (var reader = new StreamReader(Path.Combine(gtfsDir, "trips.txt")))
using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
{
    var records = csv.GetRecords<dynamic>();
    foreach (var row in records)
    {
        string routeId = row.route_id;
        string shapeId = row.shape_id;

        if (string.IsNullOrWhiteSpace(routeId) || string.IsNullOrWhiteSpace(shapeId))
            continue;

        var insertCmd = new NpgsqlCommand(@"
            INSERT INTO trips_staging (route_id, shape_id)
            VALUES (@routeId, @shapeId);", conn);
        insertCmd.Parameters.AddWithValue("routeId", routeId);
        insertCmd.Parameters.AddWithValue("shapeId", shapeId);
        await insertCmd.ExecuteNonQueryAsync();
    }
}

Console.WriteLine("Kopplar routes till shapes...");
string routeShapeSql = @"
INSERT INTO route_shapes (route_id, gtfs_shape_id)
SELECT r.id, t.shape_id
FROM routes r
JOIN (
    SELECT DISTINCT route_id, shape_id FROM trips_staging
) t ON r.gtfs_route_id = t.route_id;";
await using (var cmd = new NpgsqlCommand(routeShapeSql, conn))
    await cmd.ExecuteNonQueryAsync();

Console.WriteLine("Importen är klar ✅");
