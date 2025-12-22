using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransitTrackerWebApi.Migrations
{
    /// <inheritdoc />
    public partial class FixUserRouteProgressRouteId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_route_progress");

            migrationBuilder.CreateTable(
                name: "user_route_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_route_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_route_progress_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_route_progress_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_route_progress_route_id",
                table: "user_route_progress",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_route_progress_user_id",
                table: "user_route_progress",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_route_progress");

            migrationBuilder.CreateTable(
                name: "user_route_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    route_id1 = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_route_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_route_progress_routes_route_id1",
                        column: x => x.route_id1,
                        principalTable: "routes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_user_route_progress_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_route_progress_route_id1",
                table: "user_route_progress",
                column: "route_id1");

            migrationBuilder.CreateIndex(
                name: "ix_user_route_progress_user_id",
                table: "user_route_progress",
                column: "user_id");
        }
    }
}
