using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransitTrackerWebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteStopCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "stop_count",
                table: "routes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stop_count",
                table: "routes");
        }
    }
}
