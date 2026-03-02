using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMailDispatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mail_dispatch_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Game = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DrawDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GridsCountSent = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail_dispatch_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mail_dispatch_history_newsletter_subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "newsletter_subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mail_dispatch_history_SubscriberId_Game_DrawDate",
                table: "mail_dispatch_history",
                columns: new[] { "SubscriberId", "Game", "DrawDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mail_dispatch_history");
        }
    }
}
