using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subscriptions_Email",
                table: "subscriptions");

            migrationBuilder.RenameColumn(
                name: "UnsubscribeToken",
                table: "subscriptions",
                newName: "UnsubTokenHash");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "subscriptions",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_subscriptions_UnsubscribeToken",
                table: "subscriptions",
                newName: "IX_subscriptions_UnsubTokenHash");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.Sql("""
CREATE EXTENSION IF NOT EXISTS pgcrypto;
""");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "subscriptions",
                type: "citext",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmTokenHash",
                table: "subscriptions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Game",
                table: "subscriptions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GridCount",
                table: "subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastSentForDrawDate",
                table: "subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "subscriptions",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Strategy",
                table: "subscriptions",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnsubscribedAt",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
UPDATE subscriptions
SET
    "Game" = 'Loto',
    "GridCount" = COALESCE("GridCount", 5),
    "Strategy" = 'Uniform',
    "Status" = CASE WHEN "IsActive" THEN 'Active' ELSE 'Unsubscribed' END,
    "ConfirmedAt" = CASE WHEN "IsActive" THEN "CreatedAt" ELSE NULL END,
    "UnsubscribedAt" = CASE WHEN "IsActive" THEN NULL ELSE "CreatedAt" END,
    "ConfirmTokenHash" = encode(digest("Id"::text || ':confirm:' || "Email", 'sha256'), 'hex'),
    "UnsubTokenHash" = encode(digest("UnsubTokenHash", 'sha256'), 'hex');
""");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "subscriptions");

            migrationBuilder.CreateTable(
                name: "email_send_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntendedDrawDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_send_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_send_logs_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_ConfirmTokenHash",
                table: "subscriptions",
                column: "ConfirmTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_Email",
                table: "subscriptions",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_Email_Game_Status",
                table: "subscriptions",
                columns: new[] { "Email", "Game", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_email_send_logs_SubscriptionId_IntendedDrawDate",
                table: "email_send_logs",
                columns: new[] { "SubscriptionId", "IntendedDrawDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_send_logs");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_ConfirmTokenHash",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_Email",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_Email_Game_Status",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "ConfirmTokenHash",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "Game",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "GridCount",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "LastSentForDrawDate",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "Strategy",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "UnsubscribedAt",
                table: "subscriptions");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.RenameColumn(
                name: "UnsubTokenHash",
                table: "subscriptions",
                newName: "UnsubscribeToken");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "subscriptions",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_subscriptions_UnsubTokenHash",
                table: "subscriptions",
                newName: "IX_subscriptions_UnsubscribeToken");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "subscriptions",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 320);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_Email",
                table: "subscriptions",
                column: "Email",
                unique: true);
        }
    }
}
