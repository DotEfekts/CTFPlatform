using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTFPlatform.Migrations
{
    /// <inheritdoc />
    public partial class NormalisedDBAddedlogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengeInstances_Users_UserId",
                table: "ChallengeInstances");

            migrationBuilder.DropIndex(
                name: "IX_ChallengeInstances_UserId",
                table: "ChallengeInstances");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ChallengeInstances",
                newName: "Destroyed");

            migrationBuilder.AddColumn<string>(
                name: "LoggingInfoFormat",
                table: "Challenges",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CtfUserId",
                table: "ChallengeInstances",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoggingInfo",
                table: "ChallengeInstances",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Log",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TimeStamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KillProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    InstanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInstances_ChallengeInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "ChallengeInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserInstances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeInstances_CtfUserId",
                table: "ChallengeInstances",
                column: "CtfUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInstances_InstanceId",
                table: "UserInstances",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInstances_UserId",
                table: "UserInstances",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengeInstances_Users_CtfUserId",
                table: "ChallengeInstances",
                column: "CtfUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengeInstances_Users_CtfUserId",
                table: "ChallengeInstances");

            migrationBuilder.DropTable(
                name: "Log");

            migrationBuilder.DropTable(
                name: "UserInstances");

            migrationBuilder.DropIndex(
                name: "IX_ChallengeInstances_CtfUserId",
                table: "ChallengeInstances");

            migrationBuilder.DropColumn(
                name: "LoggingInfoFormat",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "CtfUserId",
                table: "ChallengeInstances");

            migrationBuilder.DropColumn(
                name: "LoggingInfo",
                table: "ChallengeInstances");

            migrationBuilder.RenameColumn(
                name: "Destroyed",
                table: "ChallengeInstances",
                newName: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeInstances_UserId",
                table: "ChallengeInstances",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengeInstances_Users_UserId",
                table: "ChallengeInstances",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
