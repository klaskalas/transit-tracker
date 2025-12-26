using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransitTrackerWebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionsAndFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    min_lat = table.Column<double>(type: "double precision", nullable: true),
                    min_lon = table.Column<double>(type: "double precision", nullable: true),
                    max_lat = table.Column<double>(type: "double precision", nullable: true),
                    max_lon = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feeds",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    region_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feeds", x => x.id);
                    table.ForeignKey(
                        name: "fk_feeds_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "regions",
                columns: new[] { "id", "name", "country_code", "scope", "is_active" },
                values: new object[] { 1, "Stockholm Region", "SE", 1, true });

            migrationBuilder.InsertData(
                table: "feeds",
                columns: new[] { "id", "region_id", "name", "source_url", "version", "imported_at", "scope", "is_active" },
                values: new object[] { 1, 1, "Stockholm GTFS", "manual", null, null, 1, true });

            migrationBuilder.AddColumn<int>(
                name: "feed_id",
                table: "routes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "routes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "scope_override",
                table: "routes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_routes_feed_id",
                table: "routes",
                column: "feed_id");

            migrationBuilder.CreateIndex(
                name: "ix_feeds_region_id_name",
                table: "feeds",
                columns: new[] { "region_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regions_country_code_name",
                table: "regions",
                columns: new[] { "country_code", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_routes_feeds_feed_id",
                table: "routes",
                column: "feed_id",
                principalTable: "feeds",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_routes_feeds_feed_id",
                table: "routes");

            migrationBuilder.DropTable(
                name: "feeds");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropIndex(
                name: "ix_routes_feed_id",
                table: "routes");

            migrationBuilder.DropColumn(
                name: "feed_id",
                table: "routes");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "routes");

            migrationBuilder.DropColumn(
                name: "scope_override",
                table: "routes");
        }
    }
}
