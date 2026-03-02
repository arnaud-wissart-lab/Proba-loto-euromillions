using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterSubscribers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "newsletter_subscribers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "citext", maxLength: 320, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UnsubscribeToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LotoGridsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EuroMillionsGridsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_newsletter_subscribers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_newsletter_subscribers_ConfirmToken",
                table: "newsletter_subscribers",
                column: "ConfirmToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_newsletter_subscribers_Email",
                table: "newsletter_subscribers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_newsletter_subscribers_UnsubscribeToken",
                table: "newsletter_subscribers",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "newsletter_subscribers");
        }
    }
}
