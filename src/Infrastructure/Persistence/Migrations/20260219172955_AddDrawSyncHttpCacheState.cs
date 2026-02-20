using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDrawSyncHttpCacheState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CachedArchivesJson",
                table: "sync_state",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoryPageEtag",
                table: "sync_state",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HistoryPageLastModifiedUtc",
                table: "sync_state",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedArchivesJson",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "HistoryPageEtag",
                table: "sync_state");

            migrationBuilder.DropColumn(
                name: "HistoryPageLastModifiedUtc",
                table: "sync_state");
        }
    }
}
