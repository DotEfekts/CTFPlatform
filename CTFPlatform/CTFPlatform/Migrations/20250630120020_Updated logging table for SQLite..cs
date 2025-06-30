using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTFPlatform.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedloggingtableforSQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TimeStampSqlite",
                table: "Log",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
            migrationBuilder.Sql("UPDATE Log SET TimeStampSqlite = TimeStamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeStampSqlite",
                table: "Log");
        }
    }
}
